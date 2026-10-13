using Crm.Api.Infra.Dados;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Crm.UnitTests.Infra;

public class ConexaoBancoTests
{
    [Fact]
    public void A_conexao_desliga_o_GSS_e_mantem_o_resto()
    {
        // Como o docker-compose.prod.yml monta a connection string.
        using var db = new CrmDbContext(DadosExtensions.CriarOpcoes(
            "Host=db;Port=5432;Database=crm;Username=crm;Password=senha-de-teste"));

        var conexao = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());

        Assert.Equal(GssEncryptionMode.Disable, conexao.GssEncryptionMode);
        Assert.Equal("db", conexao.Host);
        Assert.Equal(5432, conexao.Port);
        Assert.Equal("crm", conexao.Database);
        Assert.Equal("crm", conexao.Username);
        Assert.Equal("senha-de-teste", conexao.Password);
    }
}
