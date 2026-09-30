using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Configuracao;
using Crm.IntegrationTests.Infra;

namespace Crm.IntegrationTests;

/// <summary>As configurações valem para todos, então estes testes rodam sozinhos e restauram o padrão no fim.</summary>
[Collection(nameof(EstadoGlobal))]
public class ConfiguracoesTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Grava as duas configurações; sem argumentos, volta aos padrões.</summary>
    private static async Task<JsonElement> EditarAsync(HttpClient admin, bool exigirDocumento = true, bool distribuicao = true)
    {
        var resposta = await admin.PutAsJsonAsync(
            "/api/configuracoes", new { exigirDocumentoParaGanho = exigirDocumento, distribuicaoAutomatica = distribuicao }, Ct);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return await resposta.LerJsonAsync();
    }

    private static async Task<JsonElement> ObterAsync(HttpClient admin) =>
        await (await admin.GetAsync("/api/configuracoes", Ct)).LerJsonAsync();

    [Fact]
    public async Task A_exigencia_de_documento_vem_ligada_e_o_admin_desliga_pela_tela()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var leadId = await atendente.CriarLeadIdAsync();
        var ganho = (await fabrica.EtapaAsync("Ganho")).Id;

        var padrao = await ObterAsync(admin);
        var recusado = await atendente.MudarEtapaAsync(leadId, ganho);
        try
        {
            Assert.False((await EditarAsync(admin, exigirDocumento: false)).GetProperty("exigirDocumentoParaGanho").GetBoolean());
            var aceito = await atendente.MudarEtapaAsync(leadId, ganho);

            Assert.True(padrao.GetProperty("exigirDocumentoParaGanho").GetBoolean());
            Assert.Equal(HttpStatusCode.BadRequest, recusado.StatusCode);
            Assert.Equal(HttpStatusCode.OK, aceito.StatusCode);
            Assert.NotNull((await fabrica.BuscarLeadAsync(leadId)).ConvertidoEm);
        }
        finally
        {
            Assert.True((await EditarAsync(admin)).GetProperty("exigirDocumentoParaGanho").GetBoolean());
        }

        var mudancas = await fabrica.AuditoriaAsync(EntidadesAuditoria.Configuracao, ChavesConfiguracao.ExigirDocumentoParaGanho);
        Assert.Equal(
            [false, true],
            mudancas.TakeLast(2).Select(r => r.Detalhes!.RootElement.GetProperty("valor").GetBoolean()));
        Assert.All(mudancas, r => Assert.Equal(AcoesAuditoria.ConfiguracaoAlterada, r.Acao));
    }

    [Fact]
    public async Task A_distribuicao_automatica_vem_ligada_e_o_admin_desliga_pela_tela()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);

        var padrao = await ObterAsync(admin);
        try
        {
            var desligada = await EditarAsync(admin, distribuicao: false);

            Assert.True(padrao.GetProperty("distribuicaoAutomatica").GetBoolean());
            Assert.False(desligada.GetProperty("distribuicaoAutomatica").GetBoolean());
            Assert.True(desligada.GetProperty("exigirDocumentoParaGanho").GetBoolean());
            Assert.False((await ObterAsync(admin)).GetProperty("distribuicaoAutomatica").GetBoolean());
        }
        finally
        {
            Assert.True((await EditarAsync(admin)).GetProperty("distribuicaoAutomatica").GetBoolean());
        }

        var mudancas = await fabrica.AuditoriaAsync(EntidadesAuditoria.Configuracao, ChavesConfiguracao.DistribuicaoAutomatica);
        Assert.Equal(
            [false, true],
            mudancas.TakeLast(2).Select(r => r.Detalhes!.RootElement.GetProperty("valor").GetBoolean()));
    }

    [Fact]
    public async Task Salvar_sem_mudar_nada_nao_entra_na_auditoria()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var antes = (await fabrica.AuditoriaAsync(EntidadesAuditoria.Configuracao, ChavesConfiguracao.ExigirDocumentoParaGanho)).Count
            + (await fabrica.AuditoriaAsync(EntidadesAuditoria.Configuracao, ChavesConfiguracao.DistribuicaoAutomatica)).Count;

        await EditarAsync(admin);

        var depois = (await fabrica.AuditoriaAsync(EntidadesAuditoria.Configuracao, ChavesConfiguracao.ExigirDocumentoParaGanho)).Count
            + (await fabrica.AuditoriaAsync(EntidadesAuditoria.Configuracao, ChavesConfiguracao.DistribuicaoAutomatica)).Count;
        Assert.Equal(antes, depois);
    }

    [Fact]
    public async Task Configuracao_ausente_responde_400_em_vez_de_voltar_ao_padrao()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);

        var resposta = await admin.PutAsJsonAsync("/api/configuracoes", new { exigirDocumentoParaGanho = false }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var atuais = await ObterAsync(admin);
        Assert.True(atuais.GetProperty("exigirDocumentoParaGanho").GetBoolean());
        Assert.True(atuais.GetProperty("distribuicaoAutomatica").GetBoolean());
    }
}
