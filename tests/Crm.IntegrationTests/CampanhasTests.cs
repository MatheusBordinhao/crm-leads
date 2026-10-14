using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Crm.Api.Features.Leads;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Auditoria;
using Crm.IntegrationTests.Infra;

namespace Crm.IntegrationTests;

/// <summary>
/// Campanhas (Passo 6): o admin renomeia, e o nome dele deixa de ser provisório. Cada teste cria a própria campanha,
/// pela entrada do Google sem nome, como chega do formulário de lead do Google Ads.
/// </summary>
public class CampanhasTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<(int Id, string IdExterno)> CampanhaProvisoriaAsync()
    {
        var idExterno = LeadsDeTeste.IdExternoUnico();
        var entrada = await fabrica.ReceberAsync(LeadsDeTeste.Entrada(Origem.GoogleAds) with { Campanha = new ReferenciaExterna(idExterno) });
        return ((await fabrica.BuscarLeadAsync(entrada.LeadId)).CampanhaId!.Value, idExterno);
    }

    private static async Task<bool> NomeProvisorioAsync(HttpClient cliente, int id) =>
        (await (await cliente.GetAsync("/api/campanhas", Ct)).LerJsonAsync())
            .EnumerateArray()
            .Single(c => c.GetProperty("id").GetInt32() == id)
            .GetProperty("nomeProvisorio")
            .GetBoolean();

    [Fact]
    public async Task Admin_renomeia_a_campanha_provisoria_e_a_auditoria_registra()
    {
        var (id, _) = await CampanhaProvisoriaAsync();
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);

        var resposta = await admin.PutAsJsonAsync($"/api/campanhas/{id}", new { nome = "  Google | Pesquisa outubro " }, Ct);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var campanha = await resposta.LerJsonAsync();
        Assert.Equal("Google | Pesquisa outubro", campanha.GetProperty("nome").GetString());
        Assert.False(campanha.GetProperty("nomeProvisorio").GetBoolean());
        Assert.False(await NomeProvisorioAsync(admin, id));
        var registro = Assert.Single(await fabrica.AuditoriaAsync(EntidadesAuditoria.Campanha, id.ToString(CultureInfo.InvariantCulture)));
        Assert.Equal(AcoesAuditoria.CampanhaRenomeada, registro.Acao);
        Assert.Equal(["nome"], registro.Detalhes!.RootElement.GetProperty("campos").EnumerateArray().Select(c => c.GetString()));
    }

    [Fact]
    public async Task Nome_que_chega_depois_pela_integracao_nao_substitui_o_do_admin()
    {
        var (id, idExterno) = await CampanhaProvisoriaAsync();
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/campanhas/{id}", new { nome = "Nome do admin" }, Ct)).StatusCode);

        var entrada = await fabrica.ReceberAsync(LeadsDeTeste.Entrada(Origem.GoogleAds) with
        {
            Campanha = new ReferenciaExterna(idExterno, "Nome da integração"),
        });

        var campanha = (await fabrica.BuscarLeadAsync(entrada.LeadId)).Campanha!;
        Assert.Equal(id, campanha.Id);
        Assert.Equal("Nome do admin", campanha.Nome);
        Assert.False(campanha.NomeProvisorio);
    }

    [Fact]
    public async Task Nome_vazio_ou_longo_demais_e_recusado_e_a_campanha_continua_provisoria()
    {
        var (id, _) = await CampanhaProvisoriaAsync();
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);

        var vazio = await admin.PutAsJsonAsync($"/api/campanhas/{id}", new { nome = "   " }, Ct);
        var longo = await admin.PutAsJsonAsync($"/api/campanhas/{id}", new { nome = new string('a', Campanha.TamanhoMaximoNome + 1) }, Ct);

        Assert.Equal(["Informe o nome."], await vazio.LerErrosDoCampoAsync("nome"));
        Assert.Equal([$"O nome pode ter até {Campanha.TamanhoMaximoNome} caracteres."], await longo.LerErrosDoCampoAsync("nome"));
        Assert.True(await NomeProvisorioAsync(admin, id));
        Assert.Empty(await fabrica.AuditoriaAsync(EntidadesAuditoria.Campanha, id.ToString(CultureInfo.InvariantCulture)));
    }

    [Fact]
    public async Task Campanha_inexistente_responde_404()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);

        var resposta = await admin.PutAsJsonAsync($"/api/campanhas/{int.MaxValue}", new { nome = "Qualquer" }, Ct);

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Fact]
    public async Task Atendente_le_mas_nao_renomeia()
    {
        var (id, _) = await CampanhaProvisoriaAsync();
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);

        var resposta = await atendente.PutAsJsonAsync($"/api/campanhas/{id}", new { nome = "Nome da atendente" }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.True(await NomeProvisorioAsync(atendente, id));
    }
}
