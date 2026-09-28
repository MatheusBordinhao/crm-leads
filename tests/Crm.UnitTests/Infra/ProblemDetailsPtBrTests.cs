using Crm.Api.Infra.Web;

namespace Crm.UnitTests.Infra;

public class ProblemDetailsPtBrTests
{
    [Theory]
    [InlineData(404, "Not Found", "Recurso não encontrado")]
    [InlineData(400, "One or more validation errors occurred.", "Requisição inválida")]
    [InlineData(500, "An error occurred while processing your request.", "Erro interno")]
    [InlineData(403, null, "Acesso negado")]
    public void Traduz_os_titulos_padrao(int status, string? tituloAtual, string esperado)
    {
        Assert.Equal(esperado, ProblemDetailsPtBr.TituloEmPortugues(status, tituloAtual));
    }

    [Fact]
    public void Mantem_o_titulo_definido_pelo_codigo()
    {
        Assert.Equal("Lead já recebido", ProblemDetailsPtBr.TituloEmPortugues(409, "Lead já recebido"));
    }

    [Fact]
    public void Mantem_o_titulo_de_status_sem_traducao()
    {
        Assert.Equal("I'm a teapot", ProblemDetailsPtBr.TituloEmPortugues(418, "I'm a teapot"));
    }
}
