using Crm.Api.Features.Usuarios;

namespace Crm.UnitTests.Usuarios;

public class CadastroUsuarioTests
{
    private static readonly DadosEditaveisUsuario Adriana =
        new("Adriana", "adriana@example.com", Perfil.Atendente, Ativo: true, ParticipaRodizio: true);

    [Fact]
    public void Sem_mudanca_nao_ha_campos_diferentes()
    {
        Assert.Empty(Adriana.CamposDiferentes(Adriana with { }));
    }

    [Fact]
    public void Lista_os_nomes_dos_campos_que_mudaram_na_ordem_do_formulario()
    {
        var depois = Adriana with { Email = "adriana.souza@example.com", Ativo = false, Perfil = Perfil.Admin };

        Assert.Equal(["email", "perfil", "ativo"], Adriana.CamposDiferentes(depois));
    }

    [Theory]
    [InlineData("adriana@example.com", true)]
    [InlineData("matheusbordinhao11+adriana@gmail.com", true)]
    [InlineData("  ADRIANA@EXAMPLE.COM ", true)]
    [InlineData("sem-arroba", false)]
    [InlineData("sem@ponto", false)]
    [InlineData("com espaco@example.com", false)]
    public void Formato_do_email(string email, bool valido)
    {
        var resultado = new CriarUsuarioRequestValidator().Validate(
            new CriarUsuarioRequest("Adriana", email, Perfil.Atendente, ParticipaRodizio: true, "provisoria-123"));

        Assert.Equal(valido, resultado.IsValid);
    }

    [Fact]
    public void O_valor_padrao_do_enum_nao_e_um_perfil()
    {
        Assert.False(Enum.IsDefined(default(Perfil)));
        Assert.NotEqual(Perfil.Admin, default);
    }

    [Fact]
    public void Perfil_com_o_valor_padrao_e_recusado_na_criacao_e_na_edicao()
    {
        var criacao = new CriarUsuarioRequestValidator().Validate(
            new CriarUsuarioRequest("Adriana", "adriana@example.com", default, ParticipaRodizio: true, "provisoria-123"));
        var edicao = new EditarUsuarioRequestValidator().Validate(
            new EditarUsuarioRequest("Adriana", "adriana@example.com", default, Ativo: true, ParticipaRodizio: true));

        Assert.Equal("Perfil inválido.", Assert.Single(criacao.Errors).ErrorMessage);
        Assert.Equal("Perfil inválido.", Assert.Single(edicao.Errors).ErrorMessage);
    }

    [Fact]
    public void Perfil_fora_da_lista_e_invalido()
    {
        var resultado = new EditarUsuarioRequestValidator().Validate(
            new EditarUsuarioRequest("Adriana", "adriana@example.com", (Perfil)7, Ativo: true, ParticipaRodizio: true));

        Assert.Equal("Perfil inválido.", Assert.Single(resultado.Errors).ErrorMessage);
    }

    [Fact]
    public void Nome_com_mais_de_100_caracteres_e_invalido()
    {
        var resultado = new EditarUsuarioRequestValidator().Validate(
            new EditarUsuarioRequest(new string('a', 101), "adriana@example.com", Perfil.Atendente, Ativo: true, ParticipaRodizio: true));

        Assert.Equal("O nome pode ter até 100 caracteres.", Assert.Single(resultado.Errors).ErrorMessage);
    }

    [Fact]
    public void Email_e_gravado_sem_espacos_e_em_minusculas()
    {
        Assert.Equal("adriana@example.com", RegrasUsuario.NormalizarEmail("  Adriana@Example.COM "));
    }
}
