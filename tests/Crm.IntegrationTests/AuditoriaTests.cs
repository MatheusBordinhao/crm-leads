using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.Datas;
using Crm.IntegrationTests.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Crm.IntegrationTests;

public class AuditoriaTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<List<RegistroAuditoria>> RegistrosDoUsuarioAsync(Guid usuarioId)
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<CrmDbContext>();
        return await db.Auditoria
            .Where(r => r.Entidade == EntidadesAuditoria.Usuario && r.EntidadeId == usuarioId.ToString())
            .OrderBy(r => r.Id)
            .ToListAsync(Ct);
    }

    private static string? Motivo(RegistroAuditoria registro) =>
        registro.Detalhes?.RootElement.GetProperty("motivo").GetString();

    /// <summary><c>GET /api/auditoria</c> com os filtros da query string.</summary>
    private static async Task<JsonElement> AuditoriaAsync(HttpClient admin, string query)
    {
        var resposta = await admin.GetAsync($"/api/auditoria?{query}", Ct);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return await resposta.LerJsonAsync();
    }

    private static List<string?> Acoes(JsonElement lista) =>
        [.. lista.GetProperty("itens").EnumerateArray().Select(r => r.GetProperty("acao").GetString())];

    [Fact]
    public async Task Login_com_falha_e_com_sucesso_fica_na_auditoria()
    {
        var usuario = await fabrica.CriarUsuarioAsync();
        using var cliente = fabrica.CriarClienteHttps();

        await cliente.PostLoginAsync(usuario.Email, "senha-errada-123");
        await cliente.PostLoginAsync(usuario.Email, UsuariosDeTeste.SenhaPadrao);

        var registros = await RegistrosDoUsuarioAsync(usuario.Id);
        Assert.Equal([AcoesAuditoria.LoginFalha, AcoesAuditoria.LoginSucesso], registros.Select(r => r.Acao));
        Assert.All(registros, r => Assert.Equal(usuario.Id, r.UsuarioId));
        Assert.Equal("senha_incorreta", Motivo(registros[0]));
        Assert.Null(registros[1].Detalhes);
    }

    [Fact]
    public async Task Login_de_usuario_desativado_registra_o_motivo()
    {
        var usuario = await fabrica.CriarUsuarioAsync(ativo: false);
        using var cliente = fabrica.CriarClienteHttps();

        await cliente.PostLoginAsync(usuario.Email, UsuariosDeTeste.SenhaPadrao);

        var registro = Assert.Single(await RegistrosDoUsuarioAsync(usuario.Id));
        Assert.Equal(AcoesAuditoria.LoginFalha, registro.Acao);
        Assert.Equal("desativado", Motivo(registro));
    }

    [Fact]
    public async Task Login_com_email_desconhecido_registra_a_falha_sem_usuario_e_sem_o_email()
    {
        var inicio = DateTimeOffset.UtcNow;
        var email = $"desconhecido-{Guid.NewGuid():N}@example.com";
        using var cliente = fabrica.CriarClienteHttps();

        await cliente.PostLoginAsync(email, UsuariosDeTeste.SenhaPadrao);

        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<CrmDbContext>();
        var falhas = await db.Auditoria
            .Where(r => r.Acao == AcoesAuditoria.LoginFalha && r.UsuarioId == null && r.OcorridoEm >= inicio)
            .ToListAsync(Ct);
        Assert.Contains(falhas, r => Motivo(r) == "usuario_desconhecido");
        Assert.DoesNotContain(falhas, r => r.Detalhes!.RootElement.GetRawText().Contains(email, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Troca_de_senha_fica_na_auditoria()
    {
        var usuario = await fabrica.CriarUsuarioAsync(trocarSenha: true);
        using var cliente = await fabrica.LogarAsync(usuario);

        await cliente.PostAsJsonAsync(
            "/api/auth/trocar-senha", new { senhaAtual = UsuariosDeTeste.SenhaPadrao, novaSenha = "nova-senha-definitiva" }, Ct);

        var registros = await RegistrosDoUsuarioAsync(usuario.Id);
        Assert.Equal([AcoesAuditoria.LoginSucesso, AcoesAuditoria.SenhaTrocada], registros.Select(r => r.Acao));
        Assert.Equal(usuario.Id, registros[1].UsuarioId);
    }

    [Fact]
    public async Task Criacao_edicao_e_redefinicao_de_senha_ficam_na_auditoria_sem_os_valores()
    {
        var eu = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        using var admin = await fabrica.LogarAsync(eu);
        var email = $"auditado-{Guid.NewGuid():N}@example.com";

        var criacao = await admin.PostAsJsonAsync(
            "/api/usuarios",
            new { nome = "Nome Auditado", email, perfil = "Atendente", participaRodizio = false, senhaProvisoria = "provisoria-123" },
            Ct);
        var id = (await criacao.LerJsonAsync()).GetProperty("id").GetGuid();
        await admin.PutAsJsonAsync(
            $"/api/usuarios/{id}",
            new { nome = "Nome Alterado", email, perfil = "Atendente", ativo = false, participaRodizio = false },
            Ct);
        await admin.PostAsJsonAsync($"/api/usuarios/{id}/redefinir-senha", new { senhaProvisoria = "outra-provisoria" }, Ct);

        var registros = await RegistrosDoUsuarioAsync(id);
        Assert.Equal(
            [AcoesAuditoria.UsuarioCriado, AcoesAuditoria.UsuarioEditado, AcoesAuditoria.UsuarioSenhaRedefinida],
            registros.Select(r => r.Acao));
        Assert.All(registros, r => Assert.Equal(eu.Id, r.UsuarioId));
        Assert.Equal("Atendente", registros[0].Detalhes!.RootElement.GetProperty("perfil").GetString());
        Assert.Equal(
            ["nome", "ativo"],
            registros[1].Detalhes!.RootElement.GetProperty("campos").EnumerateArray().Select(c => c.GetString()));
        Assert.All(registros, r => Assert.DoesNotContain("Nome", r.Detalhes?.RootElement.GetRawText() ?? "", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_consulta_traz_do_mais_recente_para_o_mais_antigo_com_quem_fez_e_os_filtros()
    {
        var eu = await fabrica.CriarUsuarioAsync(Perfil.Admin, nome: "Admin da consulta");
        using var admin = await fabrica.LogarAsync(eu);
        var id = await admin.CriarLeadIdAsync();
        var ficha = await (await admin.GetAsync($"/api/leads/{id}", Ct)).LerJsonAsync();
        var edicao = new Dictionary<string, object?>
        {
            ["nome"] = "Maria Souza Lima",
            ["email"] = ficha.GetProperty("email").GetString(),
            ["telefone"] = null,
            ["cpfCnpj"] = null,
            ["uf"] = null,
            ["empresa"] = null,
            ["versao"] = ficha.GetProperty("versao").GetUInt32(),
        };
        (await admin.PutAsJsonAsync($"/api/leads/{id}", edicao, Ct)).EnsureSuccessStatusCode();
        (await admin.GetAsync($"/api/leads/{id}/dados-titular", Ct)).EnsureSuccessStatusCode();
        var doLead = $"entidade=lead&entidadeId={id}";
        var hoje = HorarioBrasilia.Dia(DateTimeOffset.UtcNow);

        var lista = await AuditoriaAsync(admin, doLead);
        var editado = await AuditoriaAsync(admin, $"usuarioId={eu.Id}&acao={AcoesAuditoria.LeadEditado}");
        var segundaPagina = await AuditoriaAsync(admin, $"{doLead}&tamanho=1&pagina=2");
        var noPeriodo = await AuditoriaAsync(admin, $"{doLead}&de={hoje.AddDays(-1):yyyy-MM-dd}&ate={hoje.AddDays(1):yyyy-MM-dd}");
        var depois = await AuditoriaAsync(admin, $"{doLead}&de={hoje.AddDays(2):yyyy-MM-dd}");

        Assert.Equal([AcoesAuditoria.LeadDadosExportados, AcoesAuditoria.LeadEditado, AcoesAuditoria.LeadCriado], Acoes(lista));
        Assert.Equal(3, lista.GetProperty("total").GetInt32());
        var primeiro = lista.GetProperty("itens")[0];
        Assert.Equal((eu.Id, "Admin da consulta"), (primeiro.GetProperty("usuario").GetProperty("id").GetGuid(), primeiro.GetProperty("usuario").GetProperty("nome").GetString()));
        Assert.Equal(("lead", id.ToString(CultureInfo.InvariantCulture)), (primeiro.GetProperty("entidade").GetString(), primeiro.GetProperty("entidadeId").GetString()));
        Assert.Equal(JsonValueKind.Null, primeiro.GetProperty("detalhes").ValueKind);
        var registroDaEdicao = Assert.Single(editado.GetProperty("itens").EnumerateArray());
        Assert.Equal(id.ToString(CultureInfo.InvariantCulture), registroDaEdicao.GetProperty("entidadeId").GetString());
        Assert.Equal(["nome"], registroDaEdicao.GetProperty("detalhes").GetProperty("campos").EnumerateArray().Select(c => c.GetString()));
        Assert.Equal(
            (3, 2, 1, AcoesAuditoria.LeadEditado),
            (segundaPagina.GetProperty("total").GetInt32(), segundaPagina.GetProperty("pagina").GetInt32(), segundaPagina.GetProperty("tamanho").GetInt32(), Acoes(segundaPagina).Single()));
        Assert.Equal(3, noPeriodo.GetProperty("total").GetInt32());
        Assert.Equal(0, depois.GetProperty("total").GetInt32());
    }

    [Theory]
    [InlineData("de=2026-10-10&ate=2026-10-01", "ate")]
    [InlineData("de=1999-12-31", "de")]
    [InlineData("ate=9999-12-31", "ate")]
    [InlineData("tamanho=101", "tamanho")]
    [InlineData("pagina=0", "pagina")]
    public async Task Parametros_invalidos_da_consulta_respondem_400(string query, string campo)
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);

        var resposta = await admin.GetAsync($"/api/auditoria?{query}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.NotEmpty(await resposta.LerErrosDoCampoAsync(campo));
    }

    [Fact]
    public async Task Nenhum_registro_da_auditoria_tem_email_ou_senha()
    {
        using var cliente = await fabrica.LogarComoAsync(Perfil.Atendente);
        await using var conexao = new NpgsqlConnection(fabrica.ConnectionString);
        await conexao.OpenAsync(Ct);
        await using var comando = new NpgsqlCommand(
            "SELECT count(*) FROM auditoria WHERE detalhes::text LIKE '%@%' OR detalhes::text LIKE '%senha-%'", conexao);

        var comDadoPessoal = (long)(await comando.ExecuteScalarAsync(Ct))!;

        Assert.Equal(0, comDadoPessoal);
    }
}
