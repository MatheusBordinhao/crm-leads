using Crm.Api.Features.Usuarios;
using Crm.IntegrationTests.Infra;

namespace Crm.IntegrationTests;

/// <summary>
/// Importação pelo rodízio (seções 3.3 e 3.7). Quem participa do rodízio recebe os leads de todos os testes, então
/// estes rodam sozinhos, na coleção EstadoGlobal, com o rodízio só com as pessoas de cada teste.
/// </summary>
[Collection(nameof(EstadoGlobal))]
public class ImportacaoRodizioTests(CrmApiFactory fabrica)
{
    [Fact]
    public async Task Importacao_pelo_rodizio_distribui_um_lead_de_cada_vez_e_passa_a_vez()
    {
        await using var rodizio = await RodizioIsolado.PrepararAsync(fabrica);
        var agora = DateTimeOffset.UtcNow;
        var a = await rodizio.AdicionarAsync(ultimoLead: agora.AddHours(-2));
        var b = await rodizio.AdicionarAsync(ultimoLead: agora.AddHours(-1));
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var arquivo = ImportacoesDeTeste.ArquivoDoExcel(
            ImportacoesDeTeste.LinhaExcel("Lead Um"), ImportacoesDeTeste.LinhaExcel("Lead Dois"), ImportacoesDeTeste.LinhaExcel("Lead Três"));

        var resultado = await admin.ImportarAsync(arquivo);

        var leads = await fabrica.LeadsDaImportacaoAsync(resultado.GetProperty("id").GetInt64());
        Assert.Equal([a.Id, b.Id, a.Id], leads.Select(l => l.ResponsavelId));
        Assert.Equal(0, resultado.GetProperty("semResponsavel").GetInt32());
        Assert.True((await fabrica.BuscarUsuarioAsync(b.Id)).UltimoLeadAtribuidoEm > agora);
        Assert.Equal("Você recebeu 2 leads da importação", Assert.Single(await fabrica.NotificacoesDoUsuarioAsync(a.Id)).Titulo);
        Assert.Equal("Você recebeu 1 lead da importação", Assert.Single(await fabrica.NotificacoesDoUsuarioAsync(b.Id)).Titulo);
    }

    [Fact]
    public async Task Sem_ninguem_no_rodizio_os_leads_ficam_sem_responsavel_sem_aviso_por_lead()
    {
        await using var rodizio = await RodizioIsolado.PrepararAsync(fabrica);
        var outroAdmin = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        var eu = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        using var admin = await fabrica.LogarAsync(eu);
        var arquivo = ImportacoesDeTeste.ArquivoDoExcel(ImportacoesDeTeste.LinhaExcel("Lead Um"), ImportacoesDeTeste.LinhaExcel("Lead Dois"));

        var resultado = await admin.ImportarAsync(arquivo);

        Assert.Equal(2, resultado.GetProperty("semResponsavel").GetInt32());
        var resumo = Assert.Single(await fabrica.NotificacoesDoUsuarioAsync(eu.Id), n => n.ImportacaoId == resultado.GetProperty("id").GetInt64());
        Assert.Equal("Importação concluída: 2 leads novos (2 sem responsável), 0 vinculados, 0 já importados e 0 com erro", resumo.Titulo);
        foreach (var lead in await fabrica.LeadsDaImportacaoAsync(resultado.GetProperty("id").GetInt64()))
        {
            Assert.Null(lead.ResponsavelId);
            Assert.Empty(await fabrica.NotificacoesDoUsuarioAsync(outroAdmin.Id, lead.Id));
        }
    }
}
