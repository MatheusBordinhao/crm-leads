using System.Net;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Configuracao;
using Crm.Api.Infra.Dados;
using Crm.IntegrationTests.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests;

/// <summary>A configuração <c>exigir_documento_para_ganho</c> vale para todos, então o teste roda sozinho.</summary>
[Collection(nameof(EstadoGlobal))]
public class ConfiguracaoGanhoTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task ExigirDocumentoAsync(bool exigir)
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().Configuracoes
            .Where(c => c.Chave == ChavesConfiguracao.ExigirDocumentoParaGanho)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Valor, exigir ? "true" : "false"), Ct);
    }

    [Fact]
    public async Task Com_a_configuracao_desligada_Ganho_sem_documento_e_aceito()
    {
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var leadId = await atendente.CriarLeadIdAsync();
        await ExigirDocumentoAsync(false);
        try
        {
            var resposta = await atendente.MudarEtapaAsync(leadId, (await fabrica.EtapaAsync("Ganho")).Id);

            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            Assert.NotNull((await fabrica.BuscarLeadAsync(leadId)).ConvertidoEm);
        }
        finally
        {
            await ExigirDocumentoAsync(true);
        }
    }
}
