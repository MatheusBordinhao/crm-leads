using System.Text;
using Crm.Api.Infra.Importacoes;

namespace Crm.UnitTests.Importacoes;

public class CodificacoesTests
{
    private const string Texto = "nome;empresa\nJoão Conceição;Açaí & Cia – Filial Pará";

    private static readonly Encoding Windows1252 = CriarWindows1252();

    [Fact]
    public void Utf8_com_bom()
    {
        var (texto, codificacao) = Codificacoes.Decodificar([.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(Texto)]);

        Assert.Equal(Codificacao.Utf8, codificacao);
        Assert.Equal(Texto, texto);
    }

    [Fact]
    public void Utf8_sem_bom()
    {
        var (texto, codificacao) = Codificacoes.Decodificar(Encoding.UTF8.GetBytes(Texto));

        Assert.Equal(Codificacao.Utf8, codificacao);
        Assert.Equal(Texto, texto);
    }

    [Fact]
    public void Utf16_little_endian_com_bom_como_o_da_meta()
    {
        var (texto, codificacao) = Codificacoes.Decodificar([.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(Texto)]);

        Assert.Equal(Codificacao.Utf16, codificacao);
        Assert.Equal(Texto, texto);
    }

    [Fact]
    public void Utf16_big_endian_com_bom()
    {
        var (texto, codificacao) = Codificacoes.Decodificar(
            [.. Encoding.BigEndianUnicode.GetPreamble(), .. Encoding.BigEndianUnicode.GetBytes(Texto)]);

        Assert.Equal(Codificacao.Utf16, codificacao);
        Assert.Equal(Texto, texto);
    }

    [Fact]
    public void Utf16_sem_bom_e_reconhecido_pelos_bytes_zero()
    {
        var (littleEndian, codificacaoLittle) = Codificacoes.Decodificar(Encoding.Unicode.GetBytes(Texto));
        var (bigEndian, codificacaoBig) = Codificacoes.Decodificar(Encoding.BigEndianUnicode.GetBytes(Texto));

        Assert.Equal(Codificacao.Utf16, codificacaoLittle);
        Assert.Equal(Texto, littleEndian);
        Assert.Equal(Codificacao.Utf16, codificacaoBig);
        Assert.Equal(Texto, bigEndian);
    }

    [Fact]
    public void Windows1252_do_excel_mantem_os_acentos_e_o_travessao()
    {
        var (texto, codificacao) = Codificacoes.Decodificar(Windows1252.GetBytes(Texto));

        Assert.Equal(Codificacao.Windows1252, codificacao);
        Assert.Equal(Texto, texto);
    }

    [Fact]
    public void Texto_so_com_ascii_conta_como_utf8()
    {
        var (texto, codificacao) = Codificacoes.Decodificar(Encoding.ASCII.GetBytes("nome,email\nMaria,maria@example.com"));

        Assert.Equal(Codificacao.Utf8, codificacao);
        Assert.Equal("nome,email\nMaria,maria@example.com", texto);
    }

    private static Encoding CriarWindows1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }
}
