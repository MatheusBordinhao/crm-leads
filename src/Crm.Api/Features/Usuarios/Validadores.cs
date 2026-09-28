using Crm.Api.Infra.Web;
using FluentValidation;

namespace Crm.Api.Features.Usuarios;

internal static class RegrasUsuario
{
    public static readonly string MensagemSenhaCurta =
        $"A senha precisa ter pelo menos {AcessoExtensions.TamanhoMinimoSenha} caracteres.";

    /// <summary>Senha nova: obrigatória e com o tamanho mínimo da seção 9.</summary>
    public static IRuleBuilderOptions<T, string> SenhaNova<T>(this IRuleBuilder<T, string> regra) =>
        regra
            .NotEmpty().WithMessage("Informe a senha.")
            .MinimumLength(AcessoExtensions.TamanhoMinimoSenha).WithMessage(MensagemSenhaCurta);
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
