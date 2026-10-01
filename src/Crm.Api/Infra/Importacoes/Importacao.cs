using System.Text.Json.Serialization;
using Crm.Api.Features.Leads;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Dados;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Crm.Api.Infra.Importacoes;

public enum SituacaoImportacao
{
    /// <summary>As linhas estão sendo processadas, em segundo plano.</summary>
    [JsonStringEnumMemberName("processando")]
    Processando = 1,

    [JsonStringEnumMemberName("concluida")]
    Concluida = 2,
}

/// <summary>
/// Uma importação de CSV (seção 3.7): as escolhas do admin, o andamento e o resultado. O arquivo fica guardado só
/// enquanto as linhas são processadas e é apagado no fim: os dados de cada linha ficam na entrada que ela gerou.
/// </summary>
public class Importacao
{
    public const int TamanhoMaximoNomeArquivo = 255;

    public long Id { get; set; }

    /// <summary>O admin que importou. Recebe o resumo no fim.</summary>
    public Guid UsuarioId { get; set; }

    public required string NomeArquivo { get; set; }

    public Codificacao Codificacao { get; set; }

    public Separador Separador { get; set; }

    public Origem Origem { get; set; }

    /// <summary>O campo de cada coluna do arquivo, na ordem do cabeçalho.</summary>
    public required List<CampoImportacao> Campos { get; set; }

    /// <summary>A coluna com o id do lead na origem. Sem ela, o id externo é o hash do conteúdo da linha.</summary>
    public int? ColunaIdExterno { get; set; }

    /// <summary>O id externo da campanha usada nas linhas que não trazem a sua.</summary>
    public string? CampanhaPadraoId { get; set; }

    /// <summary>O nome da campanha padrão, quando ela é nova. Nulo para uma campanha que já existe.</summary>
    public string? CampanhaPadraoNome { get; set; }

    /// <summary>Quem recebe os leads novos. Nulo: o rodízio.</summary>
    public Guid? ResponsavelId { get; set; }

    /// <summary>O arquivo como veio. Apagado quando a importação termina.</summary>
    public byte[]? Arquivo { get; set; }

    public SituacaoImportacao Situacao { get; set; }

    /// <summary>As linhas com algum valor, sem o cabeçalho.</summary>
    public int TotalLinhas { get; set; }

    public int LinhasProcessadas { get; set; }

    public int Criados { get; set; }

    public int Vinculados { get; set; }

    public int JaImportados { get; set; }

    public int ComErro { get; set; }

    /// <summary>Dos leads criados, quantos ficaram sem responsável.</summary>
    public int SemResponsavel { get; set; }

    /// <summary>
    /// Até quando a importação está reservada para quem a processa. Uma reserva vencida com a importação ainda
    /// processando quer dizer que a API parou no meio: outra rodada continua de onde ela parou.
    /// </summary>
    public DateTimeOffset ReservadaAte { get; set; }

    public DateTimeOffset CriadoEm { get; set; }

    public DateTimeOffset? ConcluidoEm { get; set; }
}

/// <summary>Uma linha que não entrou, com o motivo. Não leva dados pessoais: a linha é a mesma da planilha.</summary>
public class ErroImportacao
{
    public const int TamanhoMaximoMotivo = 200;

    public long Id { get; set; }

    public long ImportacaoId { get; set; }

    public int Linha { get; set; }

    public required string Motivo { get; set; }
}

internal sealed class ImportacaoConfiguracao : IEntityTypeConfiguration<Importacao>
{
    public void Configure(EntityTypeBuilder<Importacao> builder)
    {
        builder.ToTable("importacoes");
        builder.Property(i => i.NomeArquivo).HasMaxLength(Importacao.TamanhoMaximoNomeArquivo);
        builder.Property(i => i.Codificacao).ComoTexto(20);
        builder.Property(i => i.Separador).ComoTexto(20);
        builder.Property(i => i.Origem).ComoTexto();
        builder.PrimitiveCollection(i => i.Campos).ElementType(campo => campo.HasConversion(
            new ValueConverter<CampoImportacao, string>(c => EnumTexto<CampoImportacao>.Nome(c), t => EnumTexto<CampoImportacao>.Ler(t))));
        builder.Property(i => i.CampanhaPadraoId).HasMaxLength(Campanha.TamanhoMaximoIdExterno);
        builder.Property(i => i.CampanhaPadraoNome).HasMaxLength(Campanha.TamanhoMaximoNome);
        builder.Property(i => i.Situacao).ComoTexto(20);

        builder.HasOne<Usuario>().WithMany().HasForeignKey(i => i.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(i => i.ResponsavelId).OnDelete(DeleteBehavior.Restrict);

        // A rodada que retoma importações interrompidas procura as que ainda processam com a reserva vencida.
        builder.HasIndex(i => new { i.Situacao, i.ReservadaAte });
    }
}

internal sealed class ErroImportacaoConfiguracao : IEntityTypeConfiguration<ErroImportacao>
{
    public void Configure(EntityTypeBuilder<ErroImportacao> builder)
    {
        builder.ToTable("importacoes_erros");
        builder.Property(e => e.Motivo).HasMaxLength(ErroImportacao.TamanhoMaximoMotivo);
        builder.HasOne<Importacao>().WithMany().HasForeignKey(e => e.ImportacaoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.ImportacaoId, e.Linha });
    }
}
