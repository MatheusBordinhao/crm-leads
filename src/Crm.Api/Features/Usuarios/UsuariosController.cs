using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Features.Usuarios;

/// <summary>Cadastro de usuários, só para admin. Usuário não é excluído: é desativado.</summary>
[ApiController]
[Route("api/usuarios")]
[Authorize(Policy = Politicas.Admin)]
public sealed class UsuariosController(
    UserManager<Usuario> usuarios,
    CrmDbContext db,
    AuditoriaService auditoria,
    TimeProvider relogio) : ControllerBase
{
    public const string MensagemAutoprotecao = "Você não pode desativar a si mesmo nem tirar o seu perfil de admin.";

    private const string NomePerfilAdmin = nameof(Perfil.Admin);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UsuarioResponse>>> Listar() =>
        await Projetar(db.Users.OrderBy(u => u.Nome)).ToListAsync(HttpContext.RequestAborted);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UsuarioResponse>> Obter(Guid id)
    {
        var usuario = await BuscarAsync(id);
        return usuario is null ? NotFound() : usuario;
    }

    /// <summary>Cria o usuário com senha provisória, que ele troca no primeiro acesso.</summary>
    [HttpPost]
    [ProducesResponseType<UsuarioResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<UsuarioResponse>> Criar(CriarUsuarioRequest requisicao)
    {
        var email = RegrasUsuario.NormalizarEmail(requisicao.Email);
        var usuario = new Usuario
        {
            UserName = email,
            Email = email,
            Nome = requisicao.Nome.Trim(),
            ParticipaRodizio = requisicao.ParticipaRodizio,
            TrocarSenha = true,
            CriadoEm = relogio.GetUtcNow(),
        };

        await using var transacao = await db.Database.BeginTransactionAsync();
        var resultado = await usuarios.CreateAsync(usuario, requisicao.SenhaProvisoria);
        if (!resultado.Succeeded)
        {
            ModelState.AdicionarErros(resultado, campoDaSenha: "senhaProvisoria");
            return ValidationProblem(ModelState);
        }

        Garantir(await usuarios.AddToRoleAsync(usuario, requisicao.Perfil.ToString()));
        auditoria.Registrar(
            AcoesAuditoria.UsuarioCriado,
            EntidadesAuditoria.Usuario,
            usuario.Id.ToString(),
            new { perfil = requisicao.Perfil.ToString(), requisicao.ParticipaRodizio });
        await db.SaveChangesAsync();
        await transacao.CommitAsync();

        return CreatedAtAction(nameof(Obter), new { id = usuario.Id }, await BuscarAsync(usuario.Id));
    }

    /// <summary>Edita os dados, o perfil, a situação (ativo) e a participação no rodízio.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UsuarioResponse>> Editar(Guid id, EditarUsuarioRequest requisicao)
    {
        var usuario = await usuarios.FindByIdAsync(id.ToString());
        if (usuario is null)
        {
            return NotFound();
        }

        var ehOProprio = Guid.TryParse(usuarios.GetUserId(User), out var logado) && logado == usuario.Id;
        if (ehOProprio && (!requisicao.Ativo || requisicao.Perfil != Perfil.Admin))
        {
            return Problem(detail: MensagemAutoprotecao, statusCode: StatusCodes.Status400BadRequest);
        }

        var perfilAtual = await usuarios.ObterPerfilAsync(usuario);
        var antes = new DadosEditaveisUsuario(usuario.Nome, usuario.Email!, perfilAtual, usuario.Ativo, usuario.ParticipaRodizio);
        var depois = new DadosEditaveisUsuario(
            requisicao.Nome.Trim(),
            RegrasUsuario.NormalizarEmail(requisicao.Email),
            requisicao.Perfil,
            requisicao.Ativo,
            requisicao.ParticipaRodizio);
        var campos = antes.CamposDiferentes(depois);
        if (campos.Count == 0)
        {
            return (await BuscarAsync(id))!;
        }

        await using var transacao = await db.Database.BeginTransactionAsync();
        usuario.Nome = depois.Nome;
        usuario.Email = depois.Email;
        usuario.UserName = depois.Email;
        usuario.Ativo = depois.Ativo;
        usuario.ParticipaRodizio = depois.ParticipaRodizio;
        var resultado = await usuarios.UpdateAsync(usuario);
        if (!resultado.Succeeded)
        {
            if (resultado.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure)))
            {
                return Problem(detail: resultado.Errors.First().Description, statusCode: StatusCodes.Status409Conflict);
            }

            ModelState.AdicionarErros(resultado, campoDaSenha: string.Empty);
            return ValidationProblem(ModelState);
        }

        if (depois.Perfil != perfilAtual)
        {
            Garantir(await usuarios.RemoveFromRoleAsync(usuario, perfilAtual.ToString()));
            Garantir(await usuarios.AddToRoleAsync(usuario, depois.Perfil.ToString()));
        }

        auditoria.Registrar(AcoesAuditoria.UsuarioEditado, EntidadesAuditoria.Usuario, usuario.Id.ToString(), new { campos });
        await db.SaveChangesAsync();
        await transacao.CommitAsync();

        return (await BuscarAsync(id))!;
    }

    /// <summary>
    /// Troca a senha por uma provisória, tira o bloqueio por tentativas e encerra a sessão do usuário.
    /// </summary>
    [HttpPost("{id:guid}/redefinir-senha")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RedefinirSenha(Guid id, RedefinirSenhaRequest requisicao)
    {
        var usuario = await usuarios.FindByIdAsync(id.ToString());
        if (usuario is null)
        {
            return NotFound();
        }

        await using var transacao = await db.Database.BeginTransactionAsync();
        usuario.TrocarSenha = true;
        var token = await usuarios.GeneratePasswordResetTokenAsync(usuario);
        var resultado = await usuarios.ResetPasswordAsync(usuario, token, requisicao.SenhaProvisoria);
        if (!resultado.Succeeded)
        {
            ModelState.AdicionarErros(resultado, campoDaSenha: "senhaProvisoria");
            return ValidationProblem(ModelState);
        }

        Garantir(await usuarios.SetLockoutEndDateAsync(usuario, null));
        Garantir(await usuarios.ResetAccessFailedCountAsync(usuario));
        auditoria.Registrar(AcoesAuditoria.UsuarioSenhaRedefinida, EntidadesAuditoria.Usuario, usuario.Id.ToString());
        await db.SaveChangesAsync();
        await transacao.CommitAsync();

        return NoContent();
    }

    private Task<UsuarioResponse?> BuscarAsync(Guid id) =>
        Projetar(db.Users.Where(u => u.Id == id)).SingleOrDefaultAsync(HttpContext.RequestAborted);

    private IQueryable<UsuarioResponse> Projetar(IQueryable<Usuario> consulta)
    {
        var agora = relogio.GetUtcNow();
        var proximoDaVez = db.Users.NaOrdemDaVez().Select(u => u.Id).Take(1);
        return consulta.Select(u => new UsuarioResponse(
            u.Id,
            u.Nome,
            u.Email!,
            db.UserRoles.Any(up => up.UserId == u.Id && db.Roles.Any(p => p.Id == up.RoleId && p.Name == NomePerfilAdmin))
                ? Perfil.Admin
                : Perfil.Atendente,
            u.Ativo,
            u.ParticipaRodizio,
            u.TrocarSenha,
            u.LockoutEnd > agora ? u.LockoutEnd : null,
            u.UltimoLeadAtribuidoEm,
            proximoDaVez.Contains(u.Id),
            u.CriadoEm));
    }

    /// <summary>Falhas que não dependem do que o admin digitou: se acontecerem, é erro do sistema.</summary>
    private static void Garantir(IdentityResult resultado)
    {
        if (!resultado.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", resultado.Errors.Select(e => e.Description)));
        }
    }
}
