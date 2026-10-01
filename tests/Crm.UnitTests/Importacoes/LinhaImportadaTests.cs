using Crm.Api.Features.Leads;
using Crm.Api.Infra.Importacoes;

namespace Crm.UnitTests.Importacoes;

public class LinhaImportadaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid Admin = Guid.NewGuid();

    private static readonly string[] ColunasDaMeta =
    [
        "id", "campaign_id", "campaign_name", "adset_id", "adset_name", "ad_id", "ad_name", "form_id", "form_name", "platform",
        "full_name", "email", "phone_number", "qual_serviço_você_procura?",
    ];

    private static readonly CampoImportacao[] CamposDaMeta =
    [
        CampoImportacao.NaoImportar, CampoImportacao.CampanhaId, CampoImportacao.CampanhaNome, CampoImportacao.ConjuntoId,
        CampoImportacao.ConjuntoNome, CampoImportacao.AnuncioId, CampoImportacao.AnuncioNome, CampoImportacao.FormularioId,
        CampoImportacao.FormularioNome, CampoImportacao.Plataforma, CampoImportacao.Nome, CampoImportacao.Email,
        CampoImportacao.Telefone, CampoImportacao.RespostaExtra,
    ];

    private static readonly string[] LinhaDaMeta =
    [
        "l:1234567890", "c:120210000000001", "Outubro | Serviço Y", "as:120210000000002", "RS | 25-55", "ag:120210000000003",
        "Vídeo 1", "f:987654321", "Orçamento", "ig", "João Conceição", "joao@example.com", "p:+5551999998888", "Serviço Y",
    ];

    private static ContextoImportacao Contexto(
        IReadOnlyList<string> colunas,
        IReadOnlyList<CampoImportacao> campos,
        int? colunaIdExterno = null,
        ReferenciaExterna? campanhaPadrao = null,
        Guid? responsavelId = null) =>
        new(42, Admin, Agora, Origem.MetaLeadAds, colunas, campos, colunaIdExterno, campanhaPadrao, responsavelId);

    private static NovaEntrada Entrada(LinhaImportada linha)
    {
        Assert.Null(linha.Erro);
        return linha.Entrada!;
    }

    [Fact]
    public void Linha_da_meta_sai_sem_os_prefixos_com_a_plataforma_e_as_respostas()
    {
        var responsavel = Guid.NewGuid();
        var contexto = Contexto(ColunasDaMeta, CamposDaMeta, colunaIdExterno: 0, responsavelId: responsavel);

        var entrada = Entrada(LinhaImportada.Montar(new LinhaCsv(7, LinhaDaMeta, null), contexto));

        Assert.Equal(Origem.MetaLeadAds, entrada.Origem);
        Assert.Equal("1234567890", entrada.IdExterno);
        Assert.Equal(Agora, entrada.RecebidoEm);
        Assert.Equal("João Conceição", entrada.Nome);
        Assert.Equal("joao@example.com", entrada.Email);
        Assert.Equal("+5551999998888", entrada.Telefone);
        Assert.Equal("instagram", entrada.Plataforma);
        Assert.Equal(new ReferenciaExterna("120210000000001", "Outubro | Serviço Y"), entrada.Campanha);
        Assert.Equal(new ReferenciaExterna("120210000000002", "RS | 25-55"), entrada.Conjunto);
        Assert.Equal(new ReferenciaExterna("120210000000003", "Vídeo 1"), entrada.Anuncio);
        Assert.Equal(new ReferenciaExterna("987654321", "Orçamento"), entrada.Formulario);
        Assert.Equal("Serviço Y", Assert.Single(entrada.RespostasExtras!).Value.GetString());
        Assert.Equal("qual_serviço_você_procura?", entrada.RespostasExtras!.Keys.Single());
        Assert.Equal(responsavel, entrada.ResponsavelId);
        Assert.Equal(Admin, entrada.UsuarioId);
        Assert.Equal(42, entrada.ImportacaoId);
        Assert.False(entrada.Avisar);

        var payload = entrada.PayloadBruto!.RootElement;
        Assert.Equal(7, payload.GetProperty("linha").GetInt32());
        Assert.Equal("l:1234567890", payload.GetProperty("valores").GetProperty("id").GetString());
        Assert.Equal("ig", payload.GetProperty("valores").GetProperty("platform").GetString());
    }

    [Theory]
    [InlineData("c:abc")]
    [InlineData("x:123")]
    [InlineData("123")]
    public void Valor_sem_o_formato_do_prefixo_da_meta_fica_como_veio(string valor)
    {
        var contexto = Contexto(["nome", "email", "campanha"], [CampoImportacao.Nome, CampoImportacao.Email, CampoImportacao.CampanhaId]);

        var entrada = Entrada(LinhaImportada.Montar(new LinhaCsv(2, ["Maria", "m@example.com", valor], null), contexto));

        Assert.Equal(valor, entrada.Campanha!.Id);
    }

    [Fact]
    public void Telefone_e_email_em_formato_invalido_entram_como_vieram()
    {
        var contexto = Contexto(["nome", "email", "telefone"], [CampoImportacao.Nome, CampoImportacao.Email, CampoImportacao.Telefone]);

        var entrada = Entrada(LinhaImportada.Montar(new LinhaCsv(2, ["Maria", "maria@", "123"], null), contexto));

        Assert.Equal("maria@", entrada.Email);
        Assert.Equal("123", entrada.Telefone);
    }

    [Fact]
    public void Sem_coluna_de_id_o_id_externo_e_o_hash_da_linha_sem_depender_da_ordem_das_colunas()
    {
        var contexto = Contexto(["nome", "email", "obs"], [CampoImportacao.Nome, CampoImportacao.Email, CampoImportacao.RespostaExtra]);
        var invertido = Contexto(["obs", "email", "nome"], [CampoImportacao.RespostaExtra, CampoImportacao.Email, CampoImportacao.Nome]);

        var id = Entrada(LinhaImportada.Montar(new LinhaCsv(2, ["Maria", "m@example.com", "x"], null), contexto)).IdExterno;
        var mesmaLinhaEmOutraOrdem = Entrada(LinhaImportada.Montar(new LinhaCsv(9, ["x", " m@example.com ", "Maria"], null), invertido)).IdExterno;
        var outraLinha = Entrada(LinhaImportada.Montar(new LinhaCsv(2, ["Maria", "m@example.com", "y"], null), contexto)).IdExterno;

        Assert.Matches("^sha256:[0-9a-f]{64}$", id);
        Assert.Equal(id, mesmaLinhaEmOutraOrdem);
        Assert.NotEqual(id, outraLinha);
    }

    [Fact]
    public void Celula_de_id_vazia_usa_o_hash_da_linha()
    {
        var contexto = Contexto(["id", "nome", "email"], [CampoImportacao.NaoImportar, CampoImportacao.Nome, CampoImportacao.Email], colunaIdExterno: 0);

        var entrada = Entrada(LinhaImportada.Montar(new LinhaCsv(2, [" ", "Maria", "m@example.com"], null), contexto));

        Assert.StartsWith("sha256:", entrada.IdExterno, StringComparison.Ordinal);
    }

    [Fact]
    public void Id_externo_longo_demais_recusa_a_linha()
    {
        var contexto = Contexto(["id", "nome", "email"], [CampoImportacao.NaoImportar, CampoImportacao.Nome, CampoImportacao.Email], colunaIdExterno: 0);

        var linha = LinhaImportada.Montar(new LinhaCsv(2, [new string('9', 101), "Maria", "m@example.com"], null), contexto);

        Assert.Equal(LinhaImportada.ErroIdLongo, linha.Erro);
    }

    [Fact]
    public void Nome_junta_as_colunas_de_nome_na_ordem_do_arquivo()
    {
        var contexto = Contexto(["first_name", "email", "last_name"], [CampoImportacao.Nome, CampoImportacao.Email, CampoImportacao.Nome]);

        var entrada = Entrada(LinhaImportada.Montar(new LinhaCsv(2, [" Maria ", "m@example.com", "Souza"], null), contexto));

        Assert.Equal("Maria Souza", entrada.Nome);
    }

    [Fact]
    public void Linha_sem_nome_ou_sem_contato_e_recusada_com_o_motivo()
    {
        var contexto = Contexto(["nome", "email", "telefone"], [CampoImportacao.Nome, CampoImportacao.Email, CampoImportacao.Telefone]);

        var semNome = LinhaImportada.Montar(new LinhaCsv(2, ["  ", "m@example.com", ""], null), contexto);
        var semContato = LinhaImportada.Montar(new LinhaCsv(3, ["Maria", "", " "], null), contexto);

        Assert.Equal(LinhaImportada.ErroSemNome, semNome.Erro);
        Assert.Null(semNome.Entrada);
        Assert.Equal(LinhaImportada.ErroSemContato, semContato.Erro);
    }

    [Fact]
    public void Erro_de_estrutura_da_linha_vira_o_motivo()
    {
        var contexto = Contexto(["nome", "email"], [CampoImportacao.Nome, CampoImportacao.Email]);

        var linha = LinhaImportada.Montar(new LinhaCsv(2, ["Maria", "m@example.com"], ArquivoCsv.ErroColunasAMais), contexto);

        Assert.Equal(ArquivoCsv.ErroColunasAMais, linha.Erro);
    }

    [Fact]
    public void Campanha_da_linha_vale_mais_que_a_padrao_e_so_com_o_nome_usa_o_nome_como_id()
    {
        var padrao = new ReferenciaExterna("feira-2026", "Feira 2026");
        var contexto = Contexto(["nome", "email", "campanha"], [CampoImportacao.Nome, CampoImportacao.Email, CampoImportacao.CampanhaNome], campanhaPadrao: padrao);

        var comCampanha = Entrada(LinhaImportada.Montar(new LinhaCsv(2, ["Maria", "m@example.com", "Indicação de clientes"], null), contexto));
        var semCampanha = Entrada(LinhaImportada.Montar(new LinhaCsv(3, ["João", "j@example.com", ""], null), contexto));

        Assert.Equal(new ReferenciaExterna("Indicação de clientes", "Indicação de clientes"), comCampanha.Campanha);
        Assert.Equal(padrao, semCampanha.Campanha);
    }

    [Fact]
    public void Colunas_nao_importadas_ficam_so_no_payload_bruto()
    {
        var contexto = Contexto(["nome", "email", "vendedor"], [CampoImportacao.Nome, CampoImportacao.Email, CampoImportacao.NaoImportar]);

        var entrada = Entrada(LinhaImportada.Montar(new LinhaCsv(2, ["Maria", "m@example.com", "Carlos"], null), contexto));

        Assert.Null(entrada.RespostasExtras);
        Assert.Null(entrada.Utm);
        Assert.Equal("Carlos", entrada.PayloadBruto!.RootElement.GetProperty("valores").GetProperty("vendedor").GetString());
    }
}
