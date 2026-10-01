using System.Net;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.Importacoes;
using Crm.IntegrationTests.Infra;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests;

/// <summary>
/// Importação interrompida no meio, quando a API para (seção 3.7). O serviço em segundo plano retoma qualquer
/// importação com a reserva vencida, então estes testes usam um banco só deles e uma API sem o serviço: o teste chama o
/// processador quando quer.
/// </summary>
public class ImportacaoRetomadaTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<WebApplicationFactory<Program>> CriarApiAsync()
    {
        var banco = await fabrica.CriarBancoAsync("importacao");
        return fabrica.WithWebHostBuilder(builder =>
        {
            builder.UseSetting($"ConnectionStrings:{DadosExtensions.NomeConexao}", banco);
            builder.ConfigureTestServices(services => services.Remove(services.Single(s => s.ImplementationType == typeof(ServicoImportacoes))));
        });
    }

    /// <summary>Cria a importação pela API. Sem o serviço, ela fica processando, com a reserva de um minuto.</summary>
    private static async Task<long> CriarImportacaoAsync(HttpClient admin, byte[] arquivo)
    {
        var resposta = await admin.PostImportacaoAsync(ImportacoesDeTeste.CorpoImportacao(arquivo, await admin.AnalisarAsync(arquivo)));
        Assert.Equal(HttpStatusCode.Accepted, resposta.StatusCode);
        return (await resposta.LerJsonAsync()).GetProperty("id").GetInt64();
    }

    private static async Task VencerReservaAsync(WebApplicationFactory<Program> api, long importacaoId)
    {
        await using var escopo = api.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().Importacoes
            .Where(i => i.Id == importacaoId)
            .ExecuteUpdateAsync(i => i.SetProperty(x => x.ReservadaAte, DateTimeOffset.UtcNow.AddMinutes(-1)), Ct);
    }

    private static async Task RetomarAsync(WebApplicationFactory<Program> api)
    {
        await using var escopo = api.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<ProcessadorImportacao>().RetomarInterrompidasAsync(Ct);
    }

    [Fact]
    public async Task Importacao_com_a_reserva_vencida_e_retomada_e_concluida()
    {
        using var api = await CriarApiAsync();
        using var admin = await api.LogarComoAsync(Perfil.Admin);
        var arquivo = ImportacoesDeTeste.ArquivoDoExcel(
            ImportacoesDeTeste.LinhaExcel("Lead Um"), ImportacoesDeTeste.LinhaExcel("Lead Dois"), ImportacoesDeTeste.LinhaExcel(string.Empty));
        var id = await CriarImportacaoAsync(admin, arquivo);

        await VencerReservaAsync(api, id);
        await RetomarAsync(api);

        var resultado = await admin.AguardarConclusaoAsync(id);
        Assert.Equal(2, resultado.GetProperty("criados").GetInt32());
        Assert.Equal(1, resultado.GetProperty("comErro").GetInt32());
        Assert.Equal(3, resultado.GetProperty("linhasProcessadas").GetInt32());
        await using var escopo = api.Services.CreateAsyncScope();
        Assert.Null((await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().Importacoes.SingleAsync(i => i.Id == id, Ct)).Arquivo);
    }

    [Fact]
    public async Task Linha_gravada_antes_da_interrupcao_conta_como_criada_e_nao_como_ja_importada()
    {
        using var api = await CriarApiAsync();
        using var admin = await api.LogarComoAsync(Perfil.Admin);
        var arquivo = ImportacoesDeTeste.ArquivoDoExcel(ImportacoesDeTeste.LinhaExcel("Lead Um"), ImportacoesDeTeste.LinhaExcel("Lead Dois"));
        var id = await CriarImportacaoAsync(admin, arquivo);

        // A API parou depois de gravar a primeira linha e antes de gravar o andamento.
        await api.ReceberAsync(await PrimeiraLinhaAsync(api, id, arquivo));
        await VencerReservaAsync(api, id);
        await RetomarAsync(api);

        var resultado = await admin.AguardarConclusaoAsync(id);
        Assert.Equal(2, resultado.GetProperty("criados").GetInt32());
        Assert.Equal(0, resultado.GetProperty("jaImportados").GetInt32());
        Assert.Equal(2, (await api.LeadsDaImportacaoAsync(id)).Count);
    }

    [Fact]
    public async Task Importacao_com_a_reserva_valida_nao_e_retomada()
    {
        using var api = await CriarApiAsync();
        using var admin = await api.LogarComoAsync(Perfil.Admin);
        var id = await CriarImportacaoAsync(admin, ImportacoesDeTeste.ArquivoDoExcel(ImportacoesDeTeste.LinhaExcel("Lead Um")));

        await RetomarAsync(api);

        var importacao = await (await admin.GetAsync($"/api/importacoes/{id}", Ct)).LerJsonAsync();
        Assert.Equal("processando", importacao.GetProperty("situacao").GetString());
        Assert.Equal(0, importacao.GetProperty("linhasProcessadas").GetInt32());
    }

    /// <summary>A entrada da primeira linha, montada como o processador monta.</summary>
    private static async Task<Crm.Api.Features.Leads.NovaEntrada> PrimeiraLinhaAsync(WebApplicationFactory<Program> api, long id, byte[] arquivo)
    {
        await using var escopo = api.Services.CreateAsyncScope();
        var importacao = await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().Importacoes.AsNoTracking().SingleAsync(i => i.Id == id, Ct);
        var tabela = ArquivoCsv.Ler(arquivo).Tabela!;
        var contexto = new ContextoImportacao(
            id, importacao.UsuarioId, importacao.CriadoEm, importacao.Origem, tabela.Colunas, importacao.Campos, null, null, null);
        return LinhaImportada.Montar(tabela.Linhas[0], contexto).Entrada!;
    }
}
