using Crm.Api.Features.Funil;
using Crm.Api.Features.Leads;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Configuracao;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Infra.Dados;

public class CrmDbContext(DbContextOptions<CrmDbContext> options)
    : IdentityDbContext<Usuario, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<RegistroAuditoria> Auditoria => Set<RegistroAuditoria>();

    public DbSet<Etapa> Etapas => Set<Etapa>();

    public DbSet<MotivoPerda> MotivosPerda => Set<MotivoPerda>();

    public DbSet<ConfiguracaoSistema> Configuracoes => Set<ConfiguracaoSistema>();

    public DbSet<Lead> Leads => Set<Lead>();

    public DbSet<Campanha> Campanhas => Set<Campanha>();

    public DbSet<EntradaRecebida> EntradasRecebidas => Set<EntradaRecebida>();

    public DbSet<HistoricoEtapa> HistoricoEtapas => Set<HistoricoEtapa>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Busca sem acento (nome, empresa etc.).
        modelBuilder.HasPostgresExtension("unaccent");

        // Tabelas do Identity com nomes em português. Ficam aqui, antes das configurações por entidade, para os
        // nomes das chaves estrangeiras já saírem com os nomes novos.
        modelBuilder.Entity<Usuario>().ToTable("usuarios");
        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("perfis");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("perfis_claims");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("usuarios_perfis");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("usuarios_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("usuarios_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("usuarios_tokens");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);
    }
}
