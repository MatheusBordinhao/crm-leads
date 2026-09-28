using System.Net.Http.Json;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Dados;
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
            new { nome = "Nome Auditado", email, perfil = "Atendente", participaRodizio = true, senhaProvisoria = "provisoria-123" },
            Ct);
        var id = (await criacao.LerJsonAsync()).GetProperty("id").GetGuid();
        await admin.PutAsJsonAsync(
            $"/api/usuarios/{id}",
            new { nome = "Nome Alterado", email, perfil = "Atendente", ativo = false, participaRodizio = true },
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
