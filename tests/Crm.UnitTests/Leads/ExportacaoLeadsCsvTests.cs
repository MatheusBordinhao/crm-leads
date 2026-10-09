using System.Text;
using System.Text.Json;
using Crm.Api.Features.Funil;
using Crm.Api.Features.Leads;
using Crm.Api.Features.Leads.Normalizacao;
using Crm.Api.Features.Usuarios;

namespace Crm.UnitTests.Leads;

public class ExportacaoLeadsCsvTests
{
    private static string Texto(params Lead[] leads)
    {
        var bytes = ExportacaoLeadsCsv.Gerar(leads);
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        return Encoding.UTF8.GetString(bytes[3..]);
    }

    private static Lead Lead(string nome = "Maria Souza") => new()
    {
        Id = 1024,
        Nome = nome,
        Etapa = new Etapa { Id = 1, Nome = "Novo", Tipo = TipoEtapa.Aberta },
        Origem = Origem.Manual,
        CriadoEm = new DateTimeOffset(2026, 10, 5, 17, 3, 0, TimeSpan.Zero),
    };

    [Fact]
    public void Gera_o_cabecalho_e_uma_linha_por_lead_com_as_datas_no_horario_de_Brasilia()
    {
        var lead = Lead();
        lead.Email = "maria@empresax.com.br";
        lead.Telefone = "+5551999998888";
        lead.CpfCnpj = "52998224725";
        lead.Uf = "RS";
        lead.Empresa = "Empresa X";
        lead.Etapa = new Etapa { Id = 6, Nome = "Perdido", Tipo = TipoEtapa.Perdido };
        lead.MotivoPerda = new MotivoPerda { Id = 3, Nome = "Preço" };
        lead.Responsavel = new Usuario { Nome = "Adriana" };
        lead.Origem = Origem.MetaLeadAds;
        lead.Plataforma = "instagram";
        lead.Campanha = new Campanha { Origem = Origem.MetaLeadAds, IdExterno = "120210000000001", Nome = "Outubro | Serviço Y" };
        lead.UtmSource = "instagram";
        lead.UtmMedium = "paid";
        lead.UtmCampaign = "outubro-servico-y";
        lead.RespostasExtras = JsonDocument.Parse("""{ "Qual serviço você procura?": "Serviço Y", "Funcionários": 12 }""");
        lead.PrimeiroContatoEm = new DateTimeOffset(2026, 10, 5, 17, 20, 0, TimeSpan.Zero);
        lead.PerdidoEm = new DateTimeOffset(2026, 10, 6, 2, 30, 0, TimeSpan.Zero);

        Assert.Equal(
            "Id;Nome;E-mail;Telefone;CPF/CNPJ;UF;Empresa;Etapa;Motivo da perda;Responsável;Origem;Plataforma;Campanha;UTM source;"
            + "UTM medium;UTM campaign;UTM content;UTM term;GCLID;Respostas do formulário;Entrada;Primeiro contato;Ganho em;Perdido em;"
            + "Teste;Anonimizado em\r\n"
            + "1024;Maria Souza;maria@empresax.com.br;(51) 99999-8888;529.982.247-25;RS;Empresa X;Perdido;Preço;Adriana;Meta;instagram;"
            + "Outubro | Serviço Y;instagram;paid;outubro-servico-y;;;;Qual serviço você procura?: Serviço Y | Funcionários: 12;"
            + "05/10/2026 14:03;05/10/2026 14:20;;05/10/2026 23:30;Não;\r\n",
            Texto(lead));
    }

    [Theory]
    [InlineData("+5551999998888", "(51) 99999-8888")]
    [InlineData("+555133334444", "(51) 3333-4444")]
    [InlineData("+16502530000", "+1 650-253-0000")]
    [InlineData("(51) 9999-888", "(51) 9999-888")]
    [InlineData(null, null)]
    public void Telefone_do_Brasil_sai_no_formato_nacional_e_o_nao_reconhecido_como_foi_guardado(string? guardado, string? planilha)
    {
        Assert.Equal(planilha, Telefones.ParaLeitura(guardado));
    }

    [Theory]
    [InlineData("52998224725", "529.982.247-25")]
    [InlineData("01234567890", "012.345.678-90")]
    [InlineData("11222333000181", "11.222.333/0001-81")]
    [InlineData("12ABC34501DE35", "12.ABC.345/01DE-35")]
    [InlineData("123", "123")]
    [InlineData("123.456", "123.456")]
    [InlineData(null, null)]
    public void Documento_ganha_a_mascara_pelo_tamanho(string? guardado, string? planilha)
    {
        Assert.Equal(planilha, Documentos.ParaLeitura(guardado));
    }

    [Fact]
    public void Telefone_de_outro_pais_e_nome_que_parece_formula_ganham_o_apostrofo()
    {
        var lead = Lead("=HYPERLINK(\"https://exemplo.com\")");
        lead.Telefone = "+16502530000";

        var linha = Texto(lead).Split("\r\n")[1];

        Assert.StartsWith("1024;\"'=HYPERLINK(\"\"https://exemplo.com\"\")\";;'+1 650-253-0000;", linha, StringComparison.Ordinal);
    }

    [Fact]
    public void Lead_de_teste_e_anonimizado_sai_com_a_marca_e_a_data()
    {
        var lead = Lead(Anonimizacao.NomeDoTitularRemovido);
        lead.Teste = true;
        lead.AnonimizadoEm = new DateTimeOffset(2026, 10, 9, 15, 0, 0, TimeSpan.Zero);

        var campos = Texto(lead).Split("\r\n")[1].Split(';');

        Assert.Equal(ExportacaoLeadsCsv.Cabecalho.Length, campos.Length);
        Assert.Equal(("Titular removido", "Novo", "Cadastro manual", "Sim", "09/10/2026 12:00"), (campos[1], campos[7], campos[10], campos[^2], campos[^1]));
    }
}
