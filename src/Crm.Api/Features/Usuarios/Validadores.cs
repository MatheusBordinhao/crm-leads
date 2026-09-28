using System.Text.RegularExpressions;
using Crm.Api.Infra.Web;
using FluentValidation;

namespace Crm.Api.Features.Usuarios;

internal static partial class RegrasUsuario
{
    public const int TamanhoMaximoEmail = 256;

    public static readonly string MensagemSenhaCurta =
        $"A senha precisa ter pelo menos {AcessoExtensions.TamanhoMinimoSenha} caracteres.";

    /// <summary>E-mail do usuário como é gravado: sem espaços nas pontas e em minúsculas.</summary>
    public static string NormalizarEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>Senha nova: obrigatória e com o tamanho mínimo da seção 9.</summary>
    public static IRuleBuilderOptions<T, string> SenhaNova<T>(this IRuleBuilder<T, string> regra) =>
        regra
            .NotEmpty().WithMessage("Informe a senha.")
            .MinimumLength(AcessoExtensions.TamanhoMinimoSenha).WithMessage(MensagemSenhaCurta);

    public static IRuleBuilderOptions<T, string> NomeUsuario<T>(this IRuleBuilder<T, string> regra) =>
        regra
            .NotEmpty().WithMessage("Informe o nome.")
            .Must(n => n.Trim().Length <= Usuario.TamanhoMaximoNome)
            .WithMessage($"O nome pode ter até {Usuario.TamanhoMaximoNome} caracteres.");

    public static IRuleBuilderOptions<T, string> EmailUsuario<T>(this IRuleBuilder<T, string> regra) =>
        regra
            .NotEmpty().WithMessage("Informe o e-mail.")
            .Must(e => e.Trim().Length <= TamanhoMaximoEmail && FormatoEmail().IsMatch(e.Trim()))
            .WithMessage("E-mail inválido.");

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex FormatoEmail();
}

internal sealed class LoginRequestValidator : Validador<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().WithMessage("Informe o e-mail.");
        RuleFor(r => r.Senha).NotEmpty().WithMessage("Informe a senha.");
    }
}

internal sealed class TrocarSenhaRequestValidator : Validador<TrocarSenhaRequest>
{
    public TrocarSenhaRequestValidator()
    {
        RuleFor(r => r.SenhaAtual).NotEmpty().WithMessage("Informe a senha atual.");
        RuleFor(r => r.NovaSenha)
            .SenhaNova()
            .NotEqual(r => r.SenhaAtual).WithMessage("A nova senha precisa ser diferente da atual.");
    }
}

internal sealed class CriarUsuarioRequestValidator : Validador<CriarUsuarioRequest>
{
    public CriarUsuarioRequestValidator()
    {
        RuleFor(r => r.Nome).NomeUsuario();
        RuleFor(r => r.Email).EmailUsuario();
        RuleFor(r => r.Perfil).IsInEnum().WithMessage("Perfil inválido.");
        RuleFor(r => r.SenhaProvisoria).SenhaNova();
    }
}

internal sealed class EditarUsuarioRequestValidator : Validador<EditarUsuarioRequest>
{
    public EditarUsuarioRequestValidator()
    {
        RuleFor(r => r.Nome).NomeUsuario();
        RuleFor(r => r.Email).EmailUsuario();
        RuleFor(r => r.Perfil).IsInEnum().WithMessage("Perfil inválido.");
    }
}

internal sealed class RedefinirSenhaRequestValidator : Validador<RedefinirSenhaRequest>
{
    public RedefinirSenhaRequestValidator() => RuleFor(r => r.SenhaProvisoria).SenhaNova();
}
