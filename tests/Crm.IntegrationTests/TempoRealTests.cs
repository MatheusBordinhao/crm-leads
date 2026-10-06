using System.Net;
using System.Net.Http.Json;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.TempoReal;
using Crm.IntegrationTests.Infra;

namespace Crm.IntegrationTests;

/// <summary>
/// Critério de aceite do Passo 7: a atendente não recebe eventos de leads de outra pessoa. Cada teste conecta as
/// atendentes A e B e um admin. Depois de mexer no lead de A, um lead novo de B serve de sentinela: as mensagens de uma
/// conexão chegam em ordem, então nada do que B recebeu até a sentinela pode ter o id do lead de A.
/// </summary>
public class TempoRealTests(CrmApiFactory fabrica)
{
    private const string LeadsAlterados = nameof(IClienteTempoReal.LeadsAlterados);

    private const string NotificacaoNova = nameof(IClienteTempoReal.NotificacaoNova);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Cena> MontarAsync()
    {
        var a = await fabrica.CriarUsuarioAsync();
        var b = await fabrica.CriarUsuarioAsync();
        var admin = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        return new Cena(
            a,
            b,
            await fabrica.ConectarAsync(a),
            await fabrica.ConectarAsync(b),
            await fabrica.ConectarAsync(admin),
            await fabrica.LogarAsync(admin));
    }

    [Fact]
    public async Task Lead_novo_de_A_avisa_A_e_os_admins_e_nao_B()
    {
        await using var cena = await MontarAsync();

        var lead = await cena.CriarLeadAsync(cena.A.Id);

        await cena.ConexaoA.AguardarLeadAsync(lead);
        var notificacao = Assert.Single(await fabrica.NotificacoesDoUsuarioAsync(cena.A.Id, lead));
        await cena.ConexaoA.AguardarAsync(a => a.Nome == NotificacaoNova && a.Ids.Contains(notificacao.Id));
        await cena.ConexaoAdmin.AguardarLeadAsync(lead);
        var deB = await cena.AvisosAteASentinelaAsync(cena.B, cena.ConexaoB);
        Assert.DoesNotContain(deB, a => a.Nome == LeadsAlterados && a.Ids.Contains(lead));
        Assert.DoesNotContain(deB, a => a.Nome == NotificacaoNova && a.Ids.Contains(notificacao.Id));
    }

    [Fact]
    public async Task Mudanca_de_etapa_interacao_e_edicao_no_lead_de_A_avisam_A_e_os_admins_e_nao_B()
    {
        await using var cena = await MontarAsync();
        using var a = await fabrica.LogarAsync(cena.A);
        var corpo = LeadsDeTeste.CorpoLead();
        var lead = await cena.CriarLeadAsync(cena.A.Id, corpo);
        await cena.ConexaoA.AguardarLeadAsync(lead);
        var emContato = await fabrica.EtapaAsync("Em contato");

        await cena.AvisaAEOsAdminsAsync(lead, () => a.MudarEtapaAsync(lead, emContato.Id));
        await cena.AvisaAEOsAdminsAsync(lead, () => a.RegistrarInteracaoAsync(lead, "ligacao"));
        await cena.AvisaAEOsAdminsAsync(lead, async () =>
        {
            var versao = (await (await a.GetAsync($"/api/leads/{lead}", Ct)).LerJsonAsync()).GetProperty("versao").GetUInt32();
            var edicao = new Dictionary<string, object?>(corpo) { ["empresa"] = "Empresa nova", ["versao"] = versao };
            edicao.Remove("origem");
            edicao.Remove("responsavelId");
            return await a.PutAsJsonAsync($"/api/leads/{lead}", edicao, Ct);
        });

        var deB = await cena.AvisosAteASentinelaAsync(cena.B, cena.ConexaoB);
        Assert.DoesNotContain(deB, aviso => aviso.Nome == LeadsAlterados && aviso.Ids.Contains(lead));
    }

    [Fact]
    public async Task Lead_de_teste_so_avisa_os_admins()
    {
        await using var cena = await MontarAsync();
        using var n8n = fabrica.CriarClienteIntegracao();
        var corpo = LeadsDeTeste.CorpoIntegracao();
        corpo["teste"] = true;

        var lead = await n8n.EnviarLeadIdAsync(corpo);

        await cena.ConexaoAdmin.AguardarLeadAsync(lead);
        var deA = await cena.AvisosAteASentinelaAsync(cena.A, cena.ConexaoA);
        var deB = await cena.AvisosAteASentinelaAsync(cena.B, cena.ConexaoB);
        Assert.DoesNotContain(deA, aviso => aviso.Ids.Contains(lead) && aviso.Nome == LeadsAlterados);
        Assert.DoesNotContain(deB, aviso => aviso.Ids.Contains(lead) && aviso.Nome == LeadsAlterados);
    }

    [Fact]
    public async Task Troca_de_A_para_C_avisa_A_sem_o_id_C_com_o_id_e_a_notificacao_e_B_nada()
    {
        await using var cena = await MontarAsync();
        var c = await fabrica.CriarUsuarioAsync();
        await using var conexaoC = await fabrica.ConectarAsync(c);
        var lead = await cena.CriarLeadAsync(cena.A.Id);
        await cena.ConexaoA.AguardarLeadAsync(lead);
        var antesDaTroca = cena.ConexaoA.Avisos.Count;

        var troca = await cena.Admin.PostAsJsonAsync($"/api/leads/{lead}/responsavel", new { responsavelId = c.Id }, Ct);

        Assert.Equal(HttpStatusCode.OK, troca.StatusCode);
        await conexaoC.AguardarLeadAsync(lead);
        var notificacao = Assert.Single(await fabrica.NotificacoesDoUsuarioAsync(c.Id, lead));
        await conexaoC.AguardarAsync(aviso => aviso.Nome == NotificacaoNova && aviso.Ids.Contains(notificacao.Id));

        // A recebe o aviso sem ids, que recarrega o que ela tiver aberto, e nunca mais o id do lead, que agora é de C.
        await cena.ConexaoA.AguardarAsync(aviso => aviso.Nome == LeadsAlterados && aviso.Ids.Length == 0, antesDaTroca);
        var deA = await cena.AvisosAteASentinelaAsync(cena.A, cena.ConexaoA);
        Assert.DoesNotContain(deA.Skip(antesDaTroca), aviso => aviso.Ids.Contains(lead) && aviso.Nome == LeadsAlterados);
        var deB = await cena.AvisosAteASentinelaAsync(cena.B, cena.ConexaoB);
        Assert.DoesNotContain(deB, aviso => aviso.Ids.Contains(lead) && aviso.Nome == LeadsAlterados);
        Assert.DoesNotContain(deB, aviso => aviso.Nome == NotificacaoNova && aviso.Ids.Contains(notificacao.Id));
    }

    [Fact]
    public async Task Quando_B_cadastra_a_mao_uma_pessoa_que_ja_e_lead_de_A_so_A_e_os_admins_recebem_o_id()
    {
        await using var cena = await MontarAsync();
        using var b = await fabrica.LogarAsync(cena.B);
        var corpo = LeadsDeTeste.CorpoLead();
        var lead = await cena.CriarLeadAsync(cena.A.Id, corpo);
        await cena.ConexaoA.AguardarLeadAsync(lead);
        var antesA = cena.ConexaoA.Avisos.Count;
        var antesAdmin = cena.ConexaoAdmin.Avisos.Count;
        var repetido = LeadsDeTeste.CorpoLead("Outro Nome");
        repetido["email"] = corpo["email"];

        var resposta = await b.CriarLeadAsync(repetido);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        await cena.ConexaoA.AguardarLeadAsync(lead, antesA);
        await cena.ConexaoAdmin.AguardarLeadAsync(lead, antesAdmin);
        var deB = await cena.AvisosAteASentinelaAsync(cena.B, cena.ConexaoB);
        Assert.DoesNotContain(deB, aviso => aviso.Ids.Contains(lead) && aviso.Nome == LeadsAlterados);
    }

    [Fact]
    public async Task Uma_etapa_nova_avisa_todas_as_telas()
    {
        await using var cena = await MontarAsync();

        await cena.Admin.CriarEtapaAsync();

        await cena.ConexaoA.AguardarAsync(aviso => aviso.Nome == nameof(IClienteTempoReal.FunilAlterado));
        await cena.ConexaoB.AguardarAsync(aviso => aviso.Nome == nameof(IClienteTempoReal.FunilAlterado));
        await cena.ConexaoAdmin.AguardarAsync(aviso => aviso.Nome == nameof(IClienteTempoReal.FunilAlterado));
    }

    /// <summary>As atendentes A e B e um admin, conectados ao hub, e o admin logado na API.</summary>
    private sealed record Cena(
        Usuario A,
        Usuario B,
        ClienteTempoReal ConexaoA,
        ClienteTempoReal ConexaoB,
        ClienteTempoReal ConexaoAdmin,
        HttpClient Admin) : IAsyncDisposable
    {
        /// <summary>O admin cadastra um lead para a responsável.</summary>
        public Task<long> CriarLeadAsync(Guid responsavelId, Dictionary<string, object?>? corpo = null)
        {
            corpo ??= LeadsDeTeste.CorpoLead();
            corpo["responsavelId"] = responsavelId;
            return Admin.CriarLeadIdAsync(corpo);
        }

        /// <summary>
        /// Cadastra um lead da atendente, a sentinela, e devolve os avisos que a conexão dela recebeu antes do aviso dele.
        /// </summary>
        public async Task<List<AvisoRecebido>> AvisosAteASentinelaAsync(Usuario atendente, ClienteTempoReal conexao)
        {
            var sentinela = await CriarLeadAsync(atendente.Id);
            return await conexao.AvisosAteAsync(aviso => aviso.Nome == LeadsAlterados && aviso.Ids.Contains(sentinela));
        }

        /// <summary>A ação no lead de A chega a A e aos admins, num aviso novo.</summary>
        public async Task AvisaAEOsAdminsAsync(long lead, Func<Task<HttpResponseMessage>> acao)
        {
            var antesA = ConexaoA.Avisos.Count;
            var antesAdmin = ConexaoAdmin.Avisos.Count;

            var resposta = await acao();

            Assert.True(resposta.IsSuccessStatusCode, $"A ação respondeu {(int)resposta.StatusCode}.");
            await ConexaoA.AguardarLeadAsync(lead, antesA);
            await ConexaoAdmin.AguardarLeadAsync(lead, antesAdmin);
        }

        public async ValueTask DisposeAsync()
        {
            Admin.Dispose();
            await ConexaoA.DisposeAsync();
            await ConexaoB.DisposeAsync();
            await ConexaoAdmin.DisposeAsync();
        }
    }
}
