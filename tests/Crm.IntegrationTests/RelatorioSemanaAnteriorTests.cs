using System.Net;
using System.Text.Json;
using Crm.IntegrationTests.Infra;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Crm.IntegrationTests;

/// <summary>
/// O padrão do relatório pela integração (4.4): sem datas, a semana anterior inteira, de segunda a domingo no horário de
/// Brasília, para o fluxo semanal da Gama só chamar a rota. A API roda com o relógio parado, e os leads entram pela
/// integração numa campanha única, com o relógio andando entre um envio e outro (o <see cref="FakeTimeProvider"/> só
/// anda para a frente). Nenhum teste usa sessão: o cookie do login venceria pelo relógio da API, e o HttpClient o
/// descartaria pelo relógio de verdade (veja o <see cref="DemonstracaoTests"/>).
/// </summary>
public class RelatorioSemanaAnteriorTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Um instante de 2026 no horário de Brasília, que não tem horário de verão desde 2019.</summary>
    private static DateTimeOffset Brasilia(int mes, int dia, int hora, int minuto) =>
        new DateTimeOffset(2026, mes, dia, hora, minuto, 0, TimeSpan.FromHours(-3)).ToUniversalTime();

    private WebApplicationFactory<Program> CriarApi(FakeTimeProvider relogio) =>
        fabrica.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(relogio)));

    /// <summary>Um lead novo da campanha, pela integração, na hora em que o relógio está.</summary>
    private static async Task EnviarAsync(HttpClient n8n, object campanha)
    {
        var corpo = LeadsDeTeste.CorpoIntegracao();
        corpo["campanha"] = campanha;
        await n8n.EnviarLeadIdAsync(corpo);
    }

    /// <summary>O período do relatório pedido sem datas e os leads novos da campanha nele (0 se ela não aparece).</summary>
    private static async Task<(string? De, string? Ate, int LeadsDaCampanha)> RelatorioSemDatasAsync(HttpClient n8n, string nome)
    {
        var resposta = await n8n.GetAsync("/api/integracoes/relatorios/campanhas", Ct);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var relatorio = await resposta.LerJsonAsync();
        var leads = relatorio.GetProperty("linhas").EnumerateArray()
            .Where(l => l.GetProperty("campanha") is { ValueKind: JsonValueKind.Object } c && c.GetProperty("nome").GetString() == nome)
            .Sum(l => l.GetProperty("leadsNovos").GetInt32());
        return (relatorio.GetProperty("de").GetString(), relatorio.GetProperty("ate").GetString(), leads);
    }

    private static async Task<string?> NomeDoCsvSemDatasAsync(HttpClient n8n)
    {
        var resposta = await n8n.GetAsync("/api/integracoes/relatorios/campanhas/csv", Ct);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return resposta.Content.Headers.ContentDisposition?.FileName;
    }

    [Fact]
    public async Task Numa_segunda_as_8h_sem_datas_vem_a_semana_anterior_inteira()
    {
        var nome = $"Semana {LeadsDeTeste.Marca()}";
        var campanha = new { id = LeadsDeTeste.IdExternoUnico(), nome };
        var relogio = new FakeTimeProvider(Brasilia(10, 4, 23, 59));
        using var api = CriarApi(relogio);
        using var n8n = api.CriarClienteIntegracao();

        // O último minuto do domingo, 04/10, conta; a meia-noite da segunda, que já é desta semana, não.
        await EnviarAsync(n8n, campanha);
        relogio.SetUtcNow(Brasilia(10, 5, 0, 0));
        await EnviarAsync(n8n, campanha);
        relogio.SetUtcNow(Brasilia(10, 5, 8, 0));

        Assert.Equal(("2026-09-28", "2026-10-04", 1), await RelatorioSemDatasAsync(n8n, nome));
        Assert.Equal("relatorio-campanhas-2026-09-28-a-2026-10-04.csv", await NomeDoCsvSemDatasAsync(n8n));
    }

    [Fact]
    public async Task Numa_quarta_sem_datas_vem_a_mesma_semana_de_segunda_a_domingo()
    {
        var nome = $"Semana {LeadsDeTeste.Marca()}";
        var campanha = new { id = LeadsDeTeste.IdExternoUnico(), nome };
        var relogio = new FakeTimeProvider(Brasilia(9, 27, 23, 59));
        using var api = CriarApi(relogio);
        using var n8n = api.CriarClienteIntegracao();

        // A meia-noite da segunda, 28/09, é o começo da semana e conta; o último minuto do domingo anterior, não.
        await EnviarAsync(n8n, campanha);
        relogio.SetUtcNow(Brasilia(9, 28, 0, 0));
        await EnviarAsync(n8n, campanha);
        relogio.SetUtcNow(Brasilia(10, 7, 15, 0));

        Assert.Equal(("2026-09-28", "2026-10-04", 1), await RelatorioSemDatasAsync(n8n, nome));
        Assert.Equal("relatorio-campanhas-2026-09-28-a-2026-10-04.csv", await NomeDoCsvSemDatasAsync(n8n));
    }

    [Fact]
    public async Task Com_so_uma_das_datas_valem_as_regras_do_painel()
    {
        using var api = CriarApi(new FakeTimeProvider(Brasilia(10, 5, 8, 0)));
        using var n8n = api.CriarClienteIntegracao();

        async Task<(string?, string?)> PeriodoAsync(string query)
        {
            var resposta = await n8n.GetAsync($"/api/integracoes/relatorios/campanhas?{query}", Ct);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var relatorio = await resposta.LerJsonAsync();
            return (relatorio.GetProperty("de").GetString(), relatorio.GetProperty("ate").GetString());
        }

        // Só o fim: os 30 dias que terminam nele. Só o início: até hoje. As duas: o período pedido.
        Assert.Equal(("2026-09-01", "2026-09-30"), await PeriodoAsync("ate=2026-09-30"));
        Assert.Equal(("2026-10-01", "2026-10-05"), await PeriodoAsync("de=2026-10-01"));
        Assert.Equal(("2026-09-21", "2026-09-27"), await PeriodoAsync("de=2026-09-21&ate=2026-09-27"));
    }
}
