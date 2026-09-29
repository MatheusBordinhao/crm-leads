using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Api.Infra.Configuracao;

/// <summary>Configuração do sistema que o admin muda pela tela, guardada como texto.</summary>
public class ConfiguracaoSistema
{
    public required string Chave { get; set; }

    public required string Valor { get; set; }
}

internal static class ChavesConfiguracao
{
    /// <summary>Ligada, os leads novos vão para o rodízio (seção 3.3). Padrão: <c>true</c>.</summary>
    public const string DistribuicaoAutomatica = "distribuicao_automatica";

    /// <summary>Ligada, marcar Ganho exige CPF/CNPJ válido (seção 3.4). Padrão: <c>true</c>.</summary>
    public const string ExigirDocumentoParaGanho = "exigir_documento_para_ganho";
}

internal sealed class ConfiguracaoSistemaConfiguracao : IEntityTypeConfiguration<ConfiguracaoSistema>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoSistema> builder)
    {
        builder.ToTable("configuracoes");
        builder.HasKey(c => c.Chave);
        builder.Property(c => c.Chave).HasMaxLength(50);
        builder.Property(c => c.Valor).HasMaxLength(200);
    }
}
