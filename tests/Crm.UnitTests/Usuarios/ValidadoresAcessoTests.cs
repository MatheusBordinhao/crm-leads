using Crm.Api.Features.Usuarios;

namespace Crm.UnitTests.Usuarios;

public class ValidadoresAcessoTests
{
    [Fact]
    public void Login_exige_email_e_senha()
    {
        var resultado = new LoginRequestValidator().Validate(new LoginRequest(null!, ""));

        Assert.Equal(
            [("Email", "Informe o e-mail."), ("Senha", "Informe a senha.")],
            resultado.Errors.Select(e => (e.PropertyName, e.ErrorMessage)));
    }

    [Theory]
    [InlineData("", "Informe a senha.")]
    [InlineData("123456789", "A senha precisa ter pelo menos 10 caracteres.")]
    [InlineData("senha-atual-123", "A nova senha precisa ser diferente da atual.")]
    public void Nova_senha_precisa_ter_10_caracteres_e_ser_diferente_da_atual(string novaSenha, string erro)
    {
        var resultado = new TrocarSenhaRequestValidator().Validate(new TrocarSenhaRequest("senha-atual-123", novaSenha));

        var falha = Assert.Single(resultado.Errors);
        Assert.Equal(nameof(TrocarSenhaRequest.NovaSenha), falha.PropertyName);
        Assert.Equal(erro, falha.ErrorMessage);
    }

    [Fact]
    public void Nova_senha_com_10_caracteres_e_valida()
    {
        var resultado = new TrocarSenhaRequestValidator().Validate(new TrocarSenhaRequest("senha-atual-123", "1234567890"));

        Assert.True(resultado.IsValid);
    }
}
