using Crm.Api.Infra.TempoReal;
using Crm.IntegrationTests.Infra;

namespace Crm.IntegrationTests;

/// <summary>
/// Critério de aceite do Passo 7: um lead novo aparece para a responsável em até 5 segundos, sem recarregar a página.
/// O lead chega pela integração e vai pelo rodízio, que vale para todos: por isso o teste fica na coleção
/// <see cref="EstadoGlobal"/>, com o rodízio só com a atendente dele (<see cref="RodizioIsolado"/>).
/// </summary>
[Collection(nameof(EstadoGlobal))]
public class TempoRealRodizioTests(CrmApiFactory fabrica)
{
    private static readonly TimeSpan Limite = TimeSpan.FromSeconds(5);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Lead_novo_da_integracao_chega_a_responsavel_em_ate_5_segundos()
    {
        await using var rodizio = await RodizioIsolado.PrepararAsync(fabrica);
        var atendente = await rodizio.AdicionarAsync();
        await using var conexao = await fabrica.ConectarAsync(atendente);
        using var n8n = fabrica.CriarClienteIntegracao();

        var envio = conexao.Agora;
        var lead = await n8n.EnviarLeadIdAsync(LeadsDeTeste.CorpoIntegracao());

        var aviso = await conexao.AguardarLeadAsync(lead);
        var notificacao = Assert.Single(await fabrica.NotificacoesDoUsuarioAsync(atendente.Id, lead));
        var avisoDaNotificacao = await conexao.AguardarAsync(
            a => a.Nome == nameof(IClienteTempoReal.NotificacaoNova) && a.Ids.Contains(notificacao.Id));
        Assert.True(aviso.ChegouEm - envio < Limite, $"O aviso do lead chegou {(aviso.ChegouEm - envio).TotalMilliseconds:F0} ms depois do envio.");
        Assert.True(
            avisoDaNotificacao.ChegouEm - envio < Limite,
            $"O aviso da notificação chegou {(avisoDaNotificacao.ChegouEm - envio).TotalMilliseconds:F0} ms depois do envio.");

        // Com o aviso, a tela recarrega o quadro: o lead já está lá, na etapa inicial.
        using var cliente = await fabrica.LogarAsync(atendente);
        var novo = await fabrica.EtapaAsync("Novo");
        var quadro = await (await cliente.GetAsync("/api/funil", Ct)).LerJsonAsync();
        var coluna = quadro.GetProperty("colunas").EnumerateArray().Single(c => c.GetProperty("etapa").GetProperty("id").GetInt32() == novo.Id);
        Assert.Contains(coluna.GetProperty("leads").EnumerateArray(), l => l.GetProperty("id").GetInt64() == lead);
    }
}
