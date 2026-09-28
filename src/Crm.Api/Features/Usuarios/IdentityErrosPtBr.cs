using Microsoft.AspNetCore.Identity;

namespace Crm.Api.Features.Usuarios;

/// <summary>
/// Mensagens do Identity em português. Nenhuma repete o valor recebido (e-mail, nome), porque as mensagens
/// podem acabar em exceções e logs.
/// </summary>
internal sealed class IdentityErrosPtBr : IdentityErrorDescriber
{
    public override IdentityError DefaultError() =>
        Erro(nameof(DefaultError), "Ocorreu um erro inesperado.");

    public override IdentityError ConcurrencyFailure() =>
        Erro(nameof(ConcurrencyFailure), "O registro foi alterado por outra pessoa. Recarregue e tente de novo.");

    public override IdentityError PasswordMismatch() =>
        Erro(nameof(PasswordMismatch), "Senha incorreta.");

    public override IdentityError InvalidToken() =>
        Erro(nameof(InvalidToken), "Token inválido.");

    public override IdentityError RecoveryCodeRedemptionFailed() =>
        Erro(nameof(RecoveryCodeRedemptionFailed), "Código de recuperação inválido.");

    public override IdentityError LoginAlreadyAssociated() =>
        Erro(nameof(LoginAlreadyAssociated), "Este login externo já está associado a outro usuário.");

    public override IdentityError InvalidUserName(string? userName) =>
        Erro(nameof(InvalidUserName), "E-mail inválido.");

    public override IdentityError InvalidEmail(string? email) =>
        Erro(nameof(InvalidEmail), "E-mail inválido.");

    public override IdentityError DuplicateUserName(string userName) =>
        Erro(nameof(DuplicateUserName), "Este e-mail já está em uso.");

    public override IdentityError DuplicateEmail(string email) =>
        Erro(nameof(DuplicateEmail), "Este e-mail já está em uso.");

    public override IdentityError InvalidRoleName(string? role) =>
        Erro(nameof(InvalidRoleName), "Perfil inválido.");

    public override IdentityError DuplicateRoleName(string role) =>
        Erro(nameof(DuplicateRoleName), "Este perfil já existe.");

    public override IdentityError UserAlreadyHasPassword() =>
        Erro(nameof(UserAlreadyHasPassword), "O usuário já tem senha.");

    public override IdentityError UserLockoutNotEnabled() =>
        Erro(nameof(UserLockoutNotEnabled), "O bloqueio por tentativas não está habilitado para este usuário.");

    public override IdentityError UserAlreadyInRole(string role) =>
        Erro(nameof(UserAlreadyInRole), "O usuário já tem este perfil.");

    public override IdentityError UserNotInRole(string role) =>
        Erro(nameof(UserNotInRole), "O usuário não tem este perfil.");

    public override IdentityError PasswordTooShort(int length) =>
        Erro(nameof(PasswordTooShort), $"A senha precisa ter pelo menos {length} caracteres.");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Erro(nameof(PasswordRequiresUniqueChars), $"A senha precisa ter pelo menos {uniqueChars} caracteres diferentes.");

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Erro(nameof(PasswordRequiresNonAlphanumeric), "A senha precisa ter pelo menos um símbolo.");

    public override IdentityError PasswordRequiresDigit() =>
        Erro(nameof(PasswordRequiresDigit), "A senha precisa ter pelo menos um número.");

    public override IdentityError PasswordRequiresLower() =>
        Erro(nameof(PasswordRequiresLower), "A senha precisa ter pelo menos uma letra minúscula.");

    public override IdentityError PasswordRequiresUpper() =>
        Erro(nameof(PasswordRequiresUpper), "A senha precisa ter pelo menos uma letra maiúscula.");

    private static IdentityError Erro(string codigo, string descricao) =>
        new() { Code = codigo, Description = descricao };
}
