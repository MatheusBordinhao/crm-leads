using Crm.Api.Features.Leads;
using Crm.Api.Infra.Importacoes;

namespace Crm.UnitTests.Importacoes;

public class SugestaoColunasTests
{
    private static TabelaCsv Tabela(string[] colunas, params string[][] linhas) =>
        new(Codificacao.Utf8, Separador.PontoEVirgula, colunas, [.. linhas.Select((valores, i) => new LinhaCsv(i + 2, valores, null))]);

    [Fact]
    public void Cabecalho_da_meta_tem_tudo_sugerido_o_id_do_lead_e_a_origem()
    {
        string[] colunas =
        [
            "id", "created_time", "ad_id", "ad_name", "adset_id", "adset_name", "campaign_id", "campaign_name", "form_id",
            "form_name", "is_organic", "platform", "full_name", "email", "phone_number", "company_name", "qual_serviço_você_procura?",
        ];
        string[] linha =
        [
            "l:1234567890", "2026-09-28T14:03:11-03:00", "ag:3", "Vídeo 1", "as:2", "RS | 25-55", "c:1", "Outubro", "f:9",
            "Orçamento", "false", "ig", "Maria", "m@example.com", "p:+5551999998888", "Empresa X", "Serviço Y",
        ];

        var sugestao = SugestaoColunas.Sugerir(Tabela(colunas, linha));

        Assert.Equal(
            [
                CampoImportacao.NaoImportar, CampoImportacao.NaoImportar, CampoImportacao.AnuncioId, CampoImportacao.AnuncioNome,
                CampoImportacao.ConjuntoId, CampoImportacao.ConjuntoNome, CampoImportacao.CampanhaId, CampoImportacao.CampanhaNome,
                CampoImportacao.FormularioId, CampoImportacao.FormularioNome, CampoImportacao.NaoImportar, CampoImportacao.Plataforma,
                CampoImportacao.Nome, CampoImportacao.Email, CampoImportacao.Telefone, CampoImportacao.Empresa,
                CampoImportacao.RespostaExtra,
            ],
            sugestao.Campos);
        Assert.Equal(0, sugestao.ColunaIdExterno);
        Assert.Equal(Origem.MetaLeadAds, sugestao.Origem);
    }

    [Fact]
    public void Cabecalho_de_planilha_em_portugues_com_acentos_e_simbolos()
    {
        string[] colunas = ["Nome", "E-mail", "Celular", "CPF/CNPJ", "Estado", "Razão Social", "Campanha", "Observações"];

        var sugestao = SugestaoColunas.Sugerir(Tabela(colunas, ["Maria", "m@example.com", "51999998888", "", "RS", "X", "Feira", "Ligar"]));

        Assert.Equal(
            [
                CampoImportacao.Nome, CampoImportacao.Email, CampoImportacao.Telefone, CampoImportacao.CpfCnpj, CampoImportacao.Uf,
                CampoImportacao.Empresa, CampoImportacao.CampanhaNome, CampoImportacao.RespostaExtra,
            ],
            sugestao.Campos);
        Assert.Null(sugestao.ColunaIdExterno);
        Assert.Equal(Origem.Planilha, sugestao.Origem);
    }

    [Fact]
    public void Nome_e_sobrenome_vao_juntos_para_o_nome_quando_nao_ha_nome_completo()
    {
        var separados = SugestaoColunas.Sugerir(Tabela(["first_name", "last_name", "email"], ["Maria", "Souza", "m@example.com"]));
        var comNomeCompleto = SugestaoColunas.Sugerir(Tabela(["first_name", "full_name", "email"], ["Maria", "Maria Souza", "m@example.com"]));

        Assert.Equal([CampoImportacao.Nome, CampoImportacao.Nome, CampoImportacao.Email], separados.Campos);
        Assert.Equal([CampoImportacao.NaoImportar, CampoImportacao.Nome, CampoImportacao.Email], comNomeCompleto.Campos);
    }

    [Fact]
    public void Segunda_coluna_do_mesmo_campo_vira_resposta_extra()
    {
        var sugestao = SugestaoColunas.Sugerir(Tabela(["Nome", "Telefone", "Celular"], ["Maria", "5133334444", "51999998888"]));

        Assert.Equal([CampoImportacao.Nome, CampoImportacao.Telefone, CampoImportacao.RespostaExtra], sugestao.Campos);
    }

    [Fact]
    public void Coluna_id_qualquer_nao_vira_id_externo_mas_lead_id_vira()
    {
        var numeroDaLinha = SugestaoColunas.Sugerir(Tabela(["id", "nome", "email"], ["1", "Maria", "m@example.com"], ["2", "João", "j@example.com"]));
        var leadId = SugestaoColunas.Sugerir(Tabela(["nome", "email", "lead_id"], ["Maria", "m@example.com", "987654321"]));

        Assert.Null(numeroDaLinha.ColunaIdExterno);
        Assert.Equal(CampoImportacao.NaoImportar, numeroDaLinha.Campos[0]);
        Assert.Equal(2, leadId.ColunaIdExterno);
        Assert.Equal(CampoImportacao.NaoImportar, leadId.Campos[2]);
    }

    [Theory]
    [InlineData("phone_number", "phone number")]
    [InlineData("E-mail", "e mail")]
    [InlineData("  CPF/CNPJ ", "cpf cnpj")]
    [InlineData("Qual serviço você procura?", "qual servico voce procura")]
    public void Nome_da_coluna_e_comparado_sem_acentos_maiusculas_e_simbolos(string nome, string simplificado)
    {
        Assert.Equal(simplificado, SugestaoColunas.Simplificar(nome));
    }
}
