using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Infra.Dados;

public class CrmDbContext(DbContextOptions<CrmDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Busca sem acento (nome, empresa etc.).
        modelBuilder.HasPostgresExtension("unaccent");
    }
}
