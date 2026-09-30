using System.Net;
using System.Net.Http.Json;
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

    private static async Task<bool> ExigirDocumentoAsync(HttpClient admin, bool exigir)
    {
        var resposta = await admin.PutAsJsonAsync("/api/configuracoes", new { exigirDocumentoParaGanho = exigir }, Ct);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return (await resposta.LerJsonAsync()).GetProperty("exigirDocumentoParaGanho").GetBoolean();
    }

    [Fact]
    public async Task A_exigencia_de_documento_vem_ligada_e_o_admin_desliga_pela_tela()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var leadId = await atendente.CriarLeadIdAsync();
        var ganho = (await fabrica.EtapaAsync("Ganho")).Id;

        var padrao = await (await admin.GetAsync("/api/configuracoes", Ct)).LerJsonAsync();
        var recusado = await atendente.MudarEtapaAsync(leadId, ganho);
        try
        {
            Assert.False(await ExigirDocumentoAsync(admin, exigir: false));
            var aceito = await atendente.MudarEtapaAsync(leadId, ganho);

            Assert.True(padrao.GetProperty("exigirDocumentoParaGanho").GetBoolean());
            Assert.Equal(HttpStatusCode.BadRequest, recusado.StatusCode);
            Assert.Equal(HttpStatusCode.OK, aceito.StatusCode);
            Assert.NotNull((await fabrica.BuscarLeadAsync(leadId)).ConvertidoEm);
        }
        finally
        {
            Assert.True(await ExigirDocumentoAsync(admin, exigir: true));
        }

        var mudancas = await fabrica.AuditoriaAsync(EntidadesAuditoria.Configuracao, ChavesConfiguracao.ExigirDocumentoParaGanho);
        Assert.Equal(
            [false, true],
            mudancas.TakeLast(2).Select(r => r.Detalhes!.RootElement.GetProperty("valor").GetBoolean()));
        Assert.All(mudancas, r => Assert.Equal(AcoesAuditoria.ConfiguracaoAlterada, r.Acao));
    }

    [Fact]
    public async Task Salvar_sem_mudar_nada_nao_entra_na_auditoria()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var antes = (await fabrica.AuditoriaAsync(EntidadesAuditoria.Configuracao, ChavesConfiguracao.ExigirDocumentoParaGanho)).Count;

        Assert.True(await ExigirDocumentoAsync(admin, exigir: true));

        Assert.Equal(antes, (await fabrica.AuditoriaAsync(EntidadesAuditoria.Configuracao, ChavesConfiguracao.ExigirDocumentoParaGanho)).Count);
    }
}
