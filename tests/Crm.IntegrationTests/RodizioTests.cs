using System.Net.Http.Json;
using Crm.Api.Features.Notificacoes;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Dados;
using Crm.IntegrationTests.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests;

/// <summary>
/// Rodízio (seção 3.3). Quem participa do rodízio recebe os leads de todos os testes, então estes rodam sozinhos,
/// na coleção EstadoGlobal, com o rodízio só com as pessoas de cada teste (<see cref="RodizioIsolado"/>).
/// </summary>
[Collection(nameof(EstadoGlobal))]
public class RodizioTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Guid?> ResponsavelAsync(long leadId) => (await fabrica.BuscarLeadAsync(leadId)).ResponsavelId;

    private static Task<long> EnviarAsync(HttpClient n8n) => n8n.EnviarLeadIdAsync(LeadsDeTeste.CorpoIntegracao());

    /// <summary>A responsável de cada lead novo, enviados um depois do outro pela integração.</summary>
    private async Task<List<Guid?>> EnviarEmSequenciaAsync(int quantidade)
    {
        using var n8n = fabrica.CriarClienteIntegracao();
        var responsaveis = new List<Guid?>();
        for (var i = 0; i < quantidade; i++)
        {
            responsaveis.Add(await ResponsavelAsync(await EnviarAsync(n8n)));
        }

        return responsaveis;
    }

    [Fact]
    public async Task Com_duas_atendentes_no_rodizio_tres_leads_seguidos_vao_para_A_B_e_A()
    {
        await using var rodizio = await RodizioIsolado.PrepararAsync(fabrica);
        var agora = DateTimeOffset.UtcNow;
        var a = await rodizio.AdicionarAsync(ultimoLead: agora.AddHours(-2));
        var b = await rodizio.AdicionarAsync(ultimoLead: agora.AddHours(-1));

        var responsaveis = await EnviarEmSequenciaAsync(3);

        Assert.Equal([a.Id, b.Id, a.Id], responsaveis);
    }

    [Fact]
    public async Task Vinte_entradas_simultaneas_nao_deixam_diferenca_maior_que_1_entre_as_duas()
    {
        await using var rodizio = await RodizioIsolado.PrepararAsync(fabrica);
        var a = await rodizio.AdicionarAsync();
        var b = await rodizio.AdicionarAsync();
        using var n8n = fabrica.CriarClienteIntegracao();

        var leads = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => EnviarAsync(n8n), Ct)));

        var responsaveis = new List<Guid?>();
        foreach (var id in leads)
        {
            responsaveis.Add(await ResponsavelAsync(id));
        }

        var daA = responsaveis.Count(r => r == a.Id);
        var daB = responsaveis.Count(r => r == b.Id);
        Assert.Equal(20, daA + daB);
        Assert.True(Math.Abs(daA - daB) <= 1, $"A ficou com {daA} leads e B com {daB}.");
    }

    [Fact]
    public async Task Sem_ninguem_no_rodizio_o_lead_fica_sem_responsavel_e_os_admins_sao_notificados()
    {
        await using var rodizio = await RodizioIsolado.PrepararAsync(fabrica);
        var admin = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        using var n8n = fabrica.CriarClienteIntegracao();

        var leadId = await EnviarAsync(n8n);

        Assert.Null(await ResponsavelAsync(leadId));
        var aviso = Assert.Single(await fabrica.NotificacoesDoUsuarioAsync(admin.Id, leadId));
        Assert.Equal(TipoNotificacao.LeadSemResponsavel, aviso.Tipo);
        Assert.Equal("Lead novo sem responsável: ninguém está no rodízio", aviso.Titulo);
    }

    [Fact]
    public async Task Com_a_distribuicao_desligada_o_lead_fica_sem_responsavel_e_os_admins_sao_notificados()
    {
        await using var rodizio = await RodizioIsolado.PrepararAsync(fabrica, distribuicaoAutomatica: false);
        var participante = await rodizio.AdicionarAsync();
        var admin = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        using var n8n = fabrica.CriarClienteIntegracao();

        var leadId = await EnviarAsync(n8n);

        Assert.Null(await ResponsavelAsync(leadId));
        Assert.Null((await fabrica.BuscarUsuarioAsync(participante.Id)).UltimoLeadAtribuidoEm);
        var aviso = Assert.Single(await fabrica.NotificacoesDoUsuarioAsync(admin.Id, leadId));
        Assert.Equal("Lead novo sem responsável: a distribuição automática está desligada", aviso.Titulo);
    }

    [Fact]
    public async Task A_pessoa_da_vez_recebe_o_aviso_de_lead_novo()
    {
        await using var rodizio = await RodizioIsolado.PrepararAsync(fabrica);
        var participante = await rodizio.AdicionarAsync();
        var admin = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        using var n8n = fabrica.CriarClienteIntegracao();

        var leadId = await EnviarAsync(n8n);

        Assert.Equal(participante.Id, await ResponsavelAsync(leadId));
        var aviso = Assert.Single(await fabrica.NotificacoesDoUsuarioAsync(participante.Id, leadId));
        Assert.Equal(TipoNotificacao.LeadAtribuido, aviso.Tipo);
        Assert.Equal("Novo lead para você", aviso.Titulo);
        Assert.Empty(await fabrica.NotificacoesDoUsuarioAsync(admin.Id, leadId));
    }

    [Fact]
    public async Task Admin_que_nao_escolhe_a_responsavel_manda_o_lead_para_o_rodizio()
    {
        await using var rodizio = await RodizioIsolado.PrepararAsync(fabrica);
        var participante = await rodizio.AdicionarAsync();
        var eu = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        using var admin = await fabrica.LogarAsync(eu);

        var leadId = await admin.CriarLeadIdAsync();

        Assert.Equal(participante.Id, await ResponsavelAsync(leadId));
        var aviso = Assert.Single(await fabrica.NotificacoesDoUsuarioAsync(participante.Id, leadId));
        Assert.Equal($"{eu.Nome} cadastrou um lead para você", aviso.Titulo);
        Assert.Empty(await fabrica.NotificacoesDoUsuarioAsync(eu.Id, leadId));
    }

    [Fact]
    public async Task So_o_rodizio_passa_a_vez()
    {
        await using var rodizio = await RodizioIsolado.PrepararAsync(fabrica);
        var agora = DateTimeOffset.UtcNow;
        var a = await rodizio.AdicionarAsync(ultimoLead: agora.AddHours(-2));
        var b = await rodizio.AdicionarAsync(ultimoLead: agora.AddHours(-1));
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        using var atendenteA = await fabrica.LogarAsync(a);
        var paraA = LeadsDeTeste.CorpoLead();
        paraA["responsavelId"] = a.Id;

        // A escolha do admin e o lead que a atendente cadastra para si não mexem na vez.
        var escolhido = await admin.CriarLeadIdAsync(paraA);
        var proprio = await atendenteA.CriarLeadIdAsync();
        var responsaveis = await EnviarEmSequenciaAsync(2);

        Assert.Equal(a.Id, await ResponsavelAsync(escolhido));
        Assert.Equal(a.Id, await ResponsavelAsync(proprio));
        Assert.Equal([a.Id, b.Id], responsaveis);
    }

    [Fact]
    public async Task Quem_esta_desativada_ou_fora_do_rodizio_nao_recebe_lead()
    {
        await using var rodizio = await RodizioIsolado.PrepararAsync(fabrica);
        var ativa = await rodizio.AdicionarAsync(ultimoLead: DateTimeOffset.UtcNow.AddHours(-1));
        var desativada = await rodizio.AdicionarAsync();
        await using (var escopo = fabrica.Services.CreateAsyncScope())
        {
            await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().Users
                .Where(u => u.Id == desativada.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.Ativo, false), Ct);
        }

        await fabrica.CriarUsuarioAsync();

        var responsaveis = await EnviarEmSequenciaAsync(2);

        Assert.Equal([ativa.Id, ativa.Id], responsaveis);
    }

    [Fact]
    public async Task Quem_nunca_recebeu_vem_primeiro_e_o_empate_e_decidido_pelo_id()
    {
        await using var rodizio = await RodizioIsolado.PrepararAsync(fabrica);
        var jaRecebeu = await rodizio.AdicionarAsync(ultimoLead: DateTimeOffset.UtcNow.AddHours(-1));
        var novas = new[] { await rodizio.AdicionarAsync(), await rodizio.AdicionarAsync() };

        // A ordem do PostgreSQL, que compara o uuid byte a byte, é a do texto do Guid.
        var nuncaReceberam = novas.OrderBy(u => u.Id.ToString(), StringComparer.Ordinal).ToList();

        var responsaveis = await EnviarEmSequenciaAsync(3);

        Assert.Equal([nuncaReceberam[0].Id, nuncaReceberam[1].Id, jaRecebeu.Id], responsaveis);
    }

    [Fact]
    public async Task Lead_de_teste_fica_fora_do_rodizio_e_nao_gera_aviso()
    {
        await using var rodizio = await RodizioIsolado.PrepararAsync(fabrica);
        var participante = await rodizio.AdicionarAsync();
        var admin = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        using var n8n = fabrica.CriarClienteIntegracao();
        var corpo = LeadsDeTeste.CorpoIntegracao();
        corpo["teste"] = true;

        var leadId = await n8n.EnviarLeadIdAsync(corpo);

        Assert.Null(await ResponsavelAsync(leadId));
        Assert.Null((await fabrica.BuscarUsuarioAsync(participante.Id)).UltimoLeadAtribuidoEm);
        Assert.Empty(await fabrica.NotificacoesDoUsuarioAsync(admin.Id, leadId));
        Assert.Empty(await fabrica.NotificacoesDoUsuarioAsync(participante.Id));
    }

    [Fact]
    public async Task Entrada_repetida_fica_com_o_lead_que_existe_sem_passar_a_vez()
    {
        await using var rodizio = await RodizioIsolado.PrepararAsync(fabrica);
        var agora = DateTimeOffset.UtcNow;
        var a = await rodizio.AdicionarAsync(ultimoLead: agora.AddHours(-2));
        var b = await rodizio.AdicionarAsync(ultimoLead: agora.AddHours(-1));
        using var n8n = fabrica.CriarClienteIntegracao();
        var corpo = LeadsDeTeste.CorpoIntegracao();
        var primeiro = await n8n.EnviarLeadIdAsync(corpo);
        var repetido = new Dictionary<string, object?>(corpo) { ["origem"] = "site", ["idExterno"] = LeadsDeTeste.IdExternoUnico() };

        var vinculado = await n8n.EnviarLeadIdAsync(repetido);
        var proximo = await EnviarAsync(n8n);

        Assert.Equal(primeiro, vinculado);
        Assert.Equal(a.Id, await ResponsavelAsync(primeiro));
        Assert.Equal(b.Id, await ResponsavelAsync(proximo));
    }

    [Fact]
    public async Task Usuario_criado_no_rodizio_pela_tela_recebe_os_leads_e_aparece_como_o_proximo_da_vez()
    {
        await using var rodizio = await RodizioIsolado.PrepararAsync(fabrica);
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var criacao = await admin.PostAsJsonAsync(
            "/api/usuarios",
            new { nome = "Adriana", email = $"rodizio-{Guid.NewGuid():N}@example.com", perfil = "Atendente", participaRodizio = true, senhaProvisoria = "provisoria-123" },
            Ct);
        var criado = await criacao.LerJsonAsync();
        var id = criado.GetProperty("id").GetGuid();
        rodizio.Acompanhar(id);
        var outra = await rodizio.AdicionarAsync(ultimoLead: DateTimeOffset.UtcNow.AddHours(-1));

        var antes = await ProximasDaVezAsync(admin);
        var responsaveis = await EnviarEmSequenciaAsync(1);
        var depois = await ProximasDaVezAsync(admin);

        Assert.True(criado.GetProperty("participaRodizio").GetBoolean());
        Assert.Equal([id], antes);
        Assert.Equal([id], responsaveis);
        Assert.Equal([outra.Id], depois);
    }

    private static async Task<List<Guid>> ProximasDaVezAsync(HttpClient admin) =>
        [.. (await (await admin.GetAsync("/api/usuarios", Ct)).LerJsonAsync()).EnumerateArray()
            .Where(u => u.GetProperty("proximoDaVez").GetBoolean())
            .Select(u => u.GetProperty("id").GetGuid())];
}
