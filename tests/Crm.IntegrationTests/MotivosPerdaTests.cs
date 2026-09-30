using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.Features.Funil;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Auditoria;
using Crm.IntegrationTests.Infra;

namespace Crm.IntegrationTests;

/// <summary>Motivos de perda: a atendente lê, o admin cadastra. Os testes só mexem nos motivos que criam.</summary>
public class MotivosPerdaTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string NomeUnico() => $"Motivo {Guid.NewGuid():N}"[..30];

    private static async Task<JsonElement> CriarAsync(HttpClient admin, string? nome = null)
    {
        var resposta = await admin.PostAsJsonAsync("/api/motivos-perda", new { nome = nome ?? NomeUnico() }, Ct);
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return await resposta.LerJsonAsync();
    }

    private static int IdDe(JsonElement motivo) => motivo.GetProperty("id").GetInt32();

    [Fact]
    public async Task Atendente_le_os_motivos_com_a_situacao()
    {
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);

        var resposta = await atendente.GetAsync("/api/motivos-perda", Ct);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var motivos = (await resposta.LerJsonAsync()).EnumerateArray().ToList();
        var semResposta = Assert.Single(motivos, m => m.GetProperty("nome").GetString() == "Sem resposta");
        Assert.True(semResposta.GetProperty("ativo").GetBoolean());
    }

    [Fact]
    public async Task Admin_cria_motivo_e_a_auditoria_registra()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var nome = NomeUnico();

        var criado = await CriarAsync(admin, $"  {nome} ");

        Assert.Equal(nome, criado.GetProperty("nome").GetString());
        Assert.True(criado.GetProperty("ativo").GetBoolean());
        var lista = await (await admin.GetAsync("/api/motivos-perda", Ct)).LerJsonAsync();
        Assert.Contains(lista.EnumerateArray(), m => IdDe(m) == IdDe(criado));
        var registro = Assert.Single(await fabrica.AuditoriaAsync(EntidadesAuditoria.MotivoPerda, IdDe(criado).ToString(CultureInfo.InvariantCulture)));
        Assert.Equal(AcoesAuditoria.MotivoPerdaCriado, registro.Acao);
    }

    [Fact]
    public async Task Nome_repetido_e_recusado_sem_diferenciar_maiusculas()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var meu = await CriarAsync(admin);

        var criar = await admin.PostAsJsonAsync("/api/motivos-perda", new { nome = "preço" }, Ct);
        var renomear = await admin.PutAsJsonAsync($"/api/motivos-perda/{IdDe(meu)}", new { nome = "SEM RESPOSTA", ativo = true }, Ct);
        var vazio = await admin.PostAsJsonAsync("/api/motivos-perda", new { nome = "" }, Ct);

        Assert.Equal([MotivosPerdaController.MensagemNomeRepetido], await criar.LerErrosDoCampoAsync("nome"));
        Assert.Equal([MotivosPerdaController.MensagemNomeRepetido], await renomear.LerErrosDoCampoAsync("nome"));
        Assert.Equal(["Informe o nome."], await vazio.LerErrosDoCampoAsync("nome"));
    }

    [Fact]
    public async Task Motivo_desativado_sai_da_escolha_e_pode_voltar()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var motivo = await CriarAsync(admin);
        var novoNome = NomeUnico();
        var perdido = (await fabrica.EtapaAsync("Perdido")).Id;
        var lead = await atendente.CriarLeadIdAsync();

        var desativacao = await admin.PutAsJsonAsync($"/api/motivos-perda/{IdDe(motivo)}", new { nome = novoNome, ativo = false }, Ct);
        var perdaComDesativado = await atendente.MudarEtapaAsync(lead, perdido, IdDe(motivo));
        var reativacao = await admin.PutAsJsonAsync($"/api/motivos-perda/{IdDe(motivo)}", new { nome = novoNome, ativo = true }, Ct);
        var perdaComReativado = await atendente.MudarEtapaAsync(lead, perdido, IdDe(motivo));

        Assert.Equal(HttpStatusCode.OK, desativacao.StatusCode);
        var desativado = await desativacao.LerJsonAsync();
        Assert.Equal((novoNome, false), (desativado.GetProperty("nome").GetString(), desativado.GetProperty("ativo").GetBoolean()));
        Assert.Equal([RegrasFunil.MensagemMotivoInvalido], await perdaComDesativado.LerErrosDoCampoAsync("motivoPerdaId"));
        Assert.Equal(HttpStatusCode.OK, reativacao.StatusCode);
        Assert.Equal(HttpStatusCode.OK, perdaComReativado.StatusCode);
        var edicoes = (await fabrica.AuditoriaAsync(EntidadesAuditoria.MotivoPerda, IdDe(motivo).ToString(CultureInfo.InvariantCulture))).Skip(1).ToList();
        Assert.Equal([AcoesAuditoria.MotivoPerdaEditado, AcoesAuditoria.MotivoPerdaEditado], edicoes.Select(r => r.Acao));
        Assert.Equal(["nome", "ativo"], edicoes[0].Detalhes!.RootElement.GetProperty("campos").EnumerateArray().Select(c => c.GetString()));
    }

    [Fact]
    public async Task Motivo_inexistente_responde_404()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);

        var resposta = await admin.PutAsJsonAsync($"/api/motivos-perda/{int.MaxValue}", new { nome = "Qualquer", ativo = true }, Ct);

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }
}
