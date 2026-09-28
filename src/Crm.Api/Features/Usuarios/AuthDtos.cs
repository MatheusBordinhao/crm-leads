namespace Crm.Api.Features.Usuarios;

public sealed record LoginRequest(string Email, string Senha);

public sealed record TrocarSenhaRequest(string SenhaAtual, string NovaSenha);

/// <summary>Usuário logado. Com <c>TrocarSenha</c>, o front leva direto à troca de senha.</summary>
public sealed record EuResponse(Guid Id, string Nome, string Email, Perfil Perfil, bool TrocarSenha);
