namespace Crm.Api.Features.Usuarios;

// Os records de requisição não imprimem os valores no ToString: o MVC imprime os argumentos da action no log
// de nível Trace, e aqui há e-mail e senha.

public sealed record LoginRequest(string Email, string Senha)
{
    public override string ToString() => nameof(LoginRequest);
}

public sealed record TrocarSenhaRequest(string SenhaAtual, string NovaSenha)
{
    public override string ToString() => nameof(TrocarSenhaRequest);
}

/// <summary>Usuário logado. Com <c>TrocarSenha</c>, o front leva direto à troca de senha.</summary>
public sealed record EuResponse(Guid Id, string Nome, string Email, Perfil Perfil, bool TrocarSenha);
