using System.Net;
using System.Text;
using Crm.Api.Features.Leads;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.Datas;
using Crm.IntegrationTests.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests;

/// <summary>Exportação da lista de leads em CSV (seção 3.7), só para o admin e com registro na auditoria.</summary>
public class ExportacaoLeadsTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>As linhas do arquivo, sem o BOM, conferindo que ele está lá.</summary>
    private static async Task<List<string>> LinhasAsync(HttpResponseMessage resposta)
    {
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var bytes = await resposta.Content.ReadAsByteArrayAsync(Ct);
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        return [.. Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries)];
    }

    private static string Campo(string linha, int coluna) => linha.Split(';')[coluna];

    [Fact]
    public async Task Exporta_os_leads_dos_filtros_da_lista_sem_paginacao_e_na_ordem_dela()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var marca = LeadsDeTeste.Marca();
        var ana = await admin.CriarLeadIdAsync(LeadsDeTeste.CorpoLead($"Ana {marca}"));
        var bia = await admin.CriarLeadIdAsync(LeadsDeTeste.CorpoLead($"Bia {marca}"));
        var carla = await admin.CriarLeadIdAsync(LeadsDeTeste.CorpoLead($"Carla {marca}"));
        var emContato = (await fabrica.EtapaAsync("Em contato")).Id;
        (await admin.MudarEtapaAsync(bia, emContato)).EnsureSuccessStatusCode();
        var hojeAntes = HorarioBrasilia.Dia(DateTimeOffset.UtcNow);

        // A página e o tamanho da lista não valem na exportação.
        var recentes = await admin.GetAsync($"/api/leads/exportacao?busca={marca}&tamanho=1&pagina=2", Ct);
        var porNome = await admin.GetAsync($"/api/leads/exportacao?busca={marca}&ordem=Nome", Ct);
        var porEtapa = await admin.GetAsync($"/api/leads/exportacao?busca={marca}&etapaId={emContato}", Ct);

        var hojeDepois = HorarioBrasilia.Dia(DateTimeOffset.UtcNow);
        Assert.Equal("text/csv", recentes.Content.Headers.ContentType?.MediaType);
        var nome = recentes.Content.Headers.ContentDisposition?.FileName;
        Assert.NotNull(nome);
        Assert.Contains(nome, new[] { $"leads-{hojeAntes:yyyy-MM-dd}.csv", $"leads-{hojeDepois:yyyy-MM-dd}.csv" });
        var linhas = await LinhasAsync(recentes);
        Assert.Equal(string.Join(';', ExportacaoLeadsCsv.Cabecalho), linhas[0]);
        Assert.Equal([carla, bia, ana], linhas[1..].Select(l => long.Parse(Campo(l, 0))));
        Assert.Equal([ana, bia, carla], (await LinhasAsync(porNome))[1..].Select(l => long.Parse(Campo(l, 0))));
        var soEmContato = Assert.Single((await LinhasAsync(porEtapa))[1..]);
        Assert.Equal(($"Bia {marca}", "Em contato"), (Campo(soEmContato, 1), Campo(soEmContato, 7)));
    }

    [Fact]
    public async Task Telefone_e_documento_saem_para_o_Excel_e_um_nome_que_parece_formula_ganha_o_apostrofo()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var marca = LeadsDeTeste.Marca();
        var telefone = LeadsDeTeste.TelefoneUnico();
        var cpf = LeadsDeTeste.CpfUnico();
        var corpo = LeadsDeTeste.CorpoLead($"=1+1 {marca}");
        corpo["telefone"] = telefone;
        corpo["cpfCnpj"] = cpf;
        corpo["empresa"] = "Açaí & Cia";
        await admin.CriarLeadIdAsync(corpo);

        var linha = Assert.Single((await LinhasAsync(await admin.GetAsync($"/api/leads/exportacao?busca={marca}", Ct)))[1..]);

        Assert.Equal($"'=1+1 {marca}", Campo(linha, 1));
        Assert.Equal((string)corpo["email"]!, Campo(linha, 2));
        Assert.Equal(LeadsDeTeste.ComoDigitado(telefone), Campo(linha, 3));
        Assert.Equal($"{cpf[..3]}.{cpf[3..6]}.{cpf[6..9]}-{cpf[9..]}", Campo(linha, 4));
        Assert.Equal("Açaí & Cia", Campo(linha, 6));
        Assert.Equal(("Novo", "Cadastro manual", "Não"), (Campo(linha, 7), Campo(linha, 10), Campo(linha, 24)));
    }

    [Fact]
    public async Task A_exportacao_entra_na_auditoria_com_os_filtros_e_sem_o_texto_da_busca()
    {
        var adminUsuario = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        using var admin = await fabrica.LogarAsync(adminUsuario);
        var marca = LeadsDeTeste.Marca();
        await admin.CriarLeadIdAsync(LeadsDeTeste.CorpoLead($"Ana {marca}"));
        await admin.CriarLeadIdAsync(LeadsDeTeste.CorpoLead($"Bia {marca}"));

        var resposta = await admin.GetAsync($"/api/leads/exportacao?busca={marca}&origem=manual&semResponsavel=true&de=2026-10-01", Ct);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var registro = await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().Auditoria
            .AsNoTracking()
            .SingleAsync(r => r.Acao == AcoesAuditoria.LeadsExportados && r.UsuarioId == adminUsuario.Id, Ct);
        Assert.Null(registro.Entidade);
        var detalhes = registro.Detalhes!.RootElement;
        Assert.Equal(2, detalhes.GetProperty("linhas").GetInt32());
        Assert.Equal(
            new Dictionary<string, string> { ["busca"] = "true", ["origem"] = "\"manual\"", ["semResponsavel"] = "true", ["de"] = "\"2026-10-01\"" },
            detalhes.GetProperty("filtros").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetRawText()));
        Assert.DoesNotContain(marca, detalhes.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task So_o_admin_exporta_e_uma_data_fora_da_faixa_responde_400()
    {
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);

        var daAtendente = await atendente.GetAsync("/api/leads/exportacao", Ct);
        var dataInvalida = await admin.GetAsync("/api/leads/exportacao?ate=9999-12-31", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, daAtendente.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, dataInvalida.StatusCode);
        Assert.Equal([HorarioBrasilia.MensagemData], await dataInvalida.LerErrosDoCampoAsync("ate"));
    }
}
