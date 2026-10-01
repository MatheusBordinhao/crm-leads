using System.Text;
using Crm.Api.Infra.Importacoes;

namespace Crm.UnitTests.Importacoes;

public class ArquivoCsvTests
{
    private static readonly Encoding Windows1252 = CriarWindows1252();

    private static TabelaCsv Ler(string texto) => Ler(Encoding.UTF8.GetBytes(texto));

    private static TabelaCsv Ler(byte[] conteudo)
    {
        var leitura = ArquivoCsv.Ler(conteudo);
        Assert.True(leitura.Lido, leitura.Erro);
        return leitura.Tabela;
    }

    private static string? Recusa(byte[] conteudo) => ArquivoCsv.Ler(conteudo).Erro;

    private static string? Recusa(string texto) => Recusa(Encoding.UTF8.GetBytes(texto));

    [Fact]
    public void Csv_da_meta_em_utf16_com_tab()
    {
        const string Texto = "id\tfull_name\tcompany_name\tphone_number\r\nl:1234567890\tJoão Conceição\tAçaí Ltda\tp:+5551999998888\r\n";

        var tabela = Ler([.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(Texto)]);

        Assert.Equal(Codificacao.Utf16, tabela.Codificacao);
        Assert.Equal(Separador.Tab, tabela.Separador);
        Assert.Equal(["id", "full_name", "company_name", "phone_number"], tabela.Colunas);
        var linha = Assert.Single(tabela.Linhas);
        Assert.Equal(2, linha.Numero);
        Assert.Equal(["l:1234567890", "João Conceição", "Açaí Ltda", "p:+5551999998888"], linha.Valores);
        Assert.Null(linha.Erro);
    }

    [Fact]
    public void Csv_do_excel_em_windows1252_com_ponto_e_virgula()
    {
        const string Texto = "Nome;Empresa;Telefone\r\nMaria Conceição;Pão de Açúcar – Centro;(51) 99999-8888\r\n";

        var tabela = Ler(Windows1252.GetBytes(Texto));

        Assert.Equal(Codificacao.Windows1252, tabela.Codificacao);
        Assert.Equal(Separador.PontoEVirgula, tabela.Separador);
        Assert.Equal(["Maria Conceição", "Pão de Açúcar – Centro", "(51) 99999-8888"], Assert.Single(tabela.Linhas).Valores);
    }

    [Fact]
    public void Csv_com_virgula()
    {
        var tabela = Ler("nome,email\nMaria,maria@example.com\n");

        Assert.Equal(Separador.Virgula, tabela.Separador);
        Assert.Equal(["Maria", "maria@example.com"], Assert.Single(tabela.Linhas).Valores);
    }

    [Fact]
    public void O_separador_e_o_que_mais_aparece_fora_de_aspas_no_cabecalho()
    {
        var tabela = Ler("\"Nome, completo\";\"E-mail, principal\";Telefone\n\"Souza, Maria\";m@example.com;51999998888\n");

        Assert.Equal(Separador.PontoEVirgula, tabela.Separador);
        Assert.Equal(["Nome, completo", "E-mail, principal", "Telefone"], tabela.Colunas);
        Assert.Equal(["Souza, Maria", "m@example.com", "51999998888"], Assert.Single(tabela.Linhas).Valores);
    }

    [Fact]
    public void A_linha_sep_do_excel_define_o_separador_e_nao_conta_na_numeracao()
    {
        var tabela = Ler("sep=;\r\na,b,c;d\r\nx;y\r\n");

        Assert.Equal(Separador.PontoEVirgula, tabela.Separador);
        Assert.Equal(["a,b,c", "d"], tabela.Colunas);
        Assert.Equal(2, Assert.Single(tabela.Linhas).Numero);
    }

    [Fact]
    public void Campo_entre_aspas_pode_ter_separador_aspas_dobradas_e_quebra_de_linha()
    {
        var tabela = Ler("nome;observacao;email\n\"Souza; Maria\";\"Disse \"\"oi\"\"\ne saiu\";maria@example.com\nJoão;ok;joao@example.com\n");

        Assert.Collection(
            tabela.Linhas,
            primeira =>
            {
                Assert.Equal(2, primeira.Numero);
                Assert.Equal(["Souza; Maria", "Disse \"oi\"\ne saiu", "maria@example.com"], primeira.Valores);
            },
            segunda =>
            {
                // A quebra de linha dentro das aspas não conta: a linha na planilha é a 3.
                Assert.Equal(3, segunda.Numero);
                Assert.Equal(["João", "ok", "joao@example.com"], segunda.Valores);
            });
    }

    [Fact]
    public void Aceita_as_quebras_de_linha_crlf_lf_e_cr()
    {
        var tabela = Ler("a;b\rx;1\ny;2\r\nz;3");

        Assert.Equal([2, 3, 4], tabela.Linhas.Select(l => l.Numero));
        Assert.Equal(["x", "y", "z"], tabela.Linhas.Select(l => l.Valores[0]));
    }

    [Fact]
    public void Linhas_vazias_sao_ignoradas_mas_contam_na_numeracao()
    {
        var tabela = Ler("nome;email\n\nMaria;m@example.com\n;;\n  ;  \nJoão;j@example.com\n\n");

        Assert.Equal([3, 6], tabela.Linhas.Select(l => l.Numero));
    }

    [Fact]
    public void Colunas_vazias_no_fim_do_cabecalho_saem_e_as_do_meio_ganham_um_nome()
    {
        var tabela = Ler("nome;;email;;\nMaria;x;m@example.com;;\n");

        Assert.Equal(["nome", "Coluna 2", "email"], tabela.Colunas);
        Assert.Equal(["Maria", "x", "m@example.com"], Assert.Single(tabela.Linhas).Valores);
        Assert.Null(tabela.Linhas[0].Erro);
    }

    [Fact]
    public void Nomes_repetidos_ganham_um_numero()
    {
        var tabela = Ler("Telefone;telefone;Nome\n1;2;Maria\n");

        Assert.Equal(["Telefone", "telefone (2)", "Nome"], tabela.Colunas);
    }

    [Fact]
    public void Valor_numa_coluna_a_mais_vira_erro_da_linha()
    {
        var tabela = Ler("nome;email\nMaria;m@example.com;sobrando\nJoão;j@example.com;;\n");

        Assert.Equal(ArquivoCsv.ErroColunasAMais, tabela.Linhas[0].Erro);
        Assert.Equal(["Maria", "m@example.com"], tabela.Linhas[0].Valores);
        Assert.Null(tabela.Linhas[1].Erro);
    }

    [Fact]
    public void Linha_com_menos_colunas_fica_com_os_valores_vazios()
    {
        var linha = Assert.Single(Ler("nome;email;telefone\nMaria\n").Linhas);

        Assert.Equal(["Maria", string.Empty, string.Empty], linha.Valores);
        Assert.Null(linha.Erro);
    }

    [Fact]
    public void Aspas_sem_fechamento_viram_erro_da_linha()
    {
        var linha = Assert.Single(Ler("nome;email\nMaria;\"m@example.com\nJoão;j@example.com\n").Linhas);

        Assert.Equal(ArquivoCsv.ErroAspas, linha.Erro);
    }

    [Fact]
    public void Aceita_5000_linhas_e_recusa_5001()
    {
        static string Arquivo(int linhas) =>
            "nome;email\n" + string.Concat(Enumerable.Range(1, linhas).Select(i => $"Lead {i};lead{i}@example.com\n"));

        Assert.Equal(5_000, Ler(Arquivo(5_000)).Linhas.Count);
        Assert.Equal(ArquivoCsv.MensagemLinhas, Recusa(Arquivo(5_001)));
    }

    [Fact]
    public void Recusa_arquivo_vazio_ou_maior_que_5_mb()
    {
        Assert.Equal(ArquivoCsv.MensagemVazio, Recusa([]));
        Assert.Equal(ArquivoCsv.MensagemTamanho, Recusa(new byte[ArquivoCsv.TamanhoMaximo + 1]));
    }

    [Fact]
    public void Recusa_planilha_do_excel_com_a_instrucao_de_salvar_como_csv()
    {
        Assert.Equal(ArquivoCsv.MensagemPlanilhaDoExcel, Recusa([0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00]));
        Assert.Equal(ArquivoCsv.MensagemPlanilhaDoExcel, Recusa([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0x00]));
    }

    [Fact]
    public void Recusa_arquivo_binario()
    {
        Assert.Equal(ArquivoCsv.MensagemNaoECsv, Recusa([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D]));
    }

    [Theory]
    [InlineData("nome\nMaria\n")]
    [InlineData("nome;\nMaria;\n")]
    [InlineData("\n\n")]
    public void Recusa_cabecalho_sem_pelo_menos_duas_colunas(string texto)
    {
        Assert.Equal(ArquivoCsv.MensagemSemColunas, Recusa(texto));
    }

    [Fact]
    public void Recusa_arquivo_so_com_o_cabecalho()
    {
        Assert.Equal(ArquivoCsv.MensagemSemLinhas, Recusa("nome;email\n;\n"));
    }

    [Fact]
    public void Recusa_mais_de_100_colunas()
    {
        var cabecalho = string.Join(';', Enumerable.Range(1, 101).Select(i => $"c{i}"));

        Assert.Equal(ArquivoCsv.MensagemColunas, Recusa($"{cabecalho}\n1;2\n"));
    }

    private static Encoding CriarWindows1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }
}
