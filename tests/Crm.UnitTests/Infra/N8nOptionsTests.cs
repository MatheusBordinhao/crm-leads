using Crm.Api.Infra.Configuracao;
using Crm.Api.Infra.Eventos;

namespace Crm.UnitTests.Infra;

public class N8nOptionsTests
{
    private const string Chave = "chave-do-webhook-com-32-caracteres-ou-mais";

    private static IReadOnlyList<string> Erros(N8nOptions opcoes) =>
        new ValidacaoN8nOptions().Validate(null, opcoes) is { Failed: true } resultado ? [.. resultado.Failures] : [];

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Sem_a_lista_vale_o_padrao_dos_alertas(string? lista)
    {
        Assert.True(N8nOptions.LerEventos(lista, out var tipos, out _));
        Assert.Equal(
            [TipoEvento.LeadCriado, TipoEvento.LeadEntradaRepetida, TipoEvento.LeadReatribuido],
            tipos.Order());
    }

    [Fact]
    public void A_lista_aceita_espacos_e_so_grava_os_tipos_dela()
    {
        var opcoes = new N8nOptions { Eventos = " lead.criado , lead.etapa_alterada," };

        Assert.True(opcoes.Grava(TipoEvento.LeadCriado));
        Assert.True(opcoes.Grava(TipoEvento.LeadEtapaAlterada));
        Assert.False(opcoes.Grava(TipoEvento.LeadReatribuido));
        Assert.Empty(Erros(opcoes));
    }

    [Fact]
    public void Tipo_desconhecido_nao_deixa_a_api_subir()
    {
        var erro = Assert.Single(Erros(new N8nOptions { Eventos = "lead.criado,lead.ganho" }));

        Assert.Contains("lead.ganho", erro, StringComparison.Ordinal);
        Assert.Contains("lead.etapa_alterada", erro, StringComparison.Ordinal);
    }

    [Fact]
    public void Sem_url_a_chave_pode_ficar_vazia()
    {
        Assert.Empty(Erros(new N8nOptions()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("curta")]
    public void Com_url_a_chave_precisa_de_32_caracteres(string? chave)
    {
        var opcoes = new N8nOptions { WebhookUrl = "https://exemplo.app.n8n.cloud/webhook/crm-alertas", WebhookChave = chave };

        Assert.Equal([ValidacaoN8nOptions.MensagemChave], Erros(opcoes));
    }

    [Theory]
    [InlineData("exemplo.app.n8n.cloud/webhook/crm-alertas")]
    [InlineData("ftp://exemplo.app.n8n.cloud/webhook")]
    public void A_url_precisa_ser_completa(string url)
    {
        Assert.Equal([ValidacaoN8nOptions.MensagemUrl], Erros(new N8nOptions { WebhookUrl = url, WebhookChave = Chave }));
    }

    [Fact]
    public void Configuracao_completa_passa()
    {
        var opcoes = new N8nOptions { WebhookUrl = "https://exemplo.app.n8n.cloud/webhook/crm-alertas", WebhookChave = Chave };

        Assert.Empty(Erros(opcoes));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("crm.empresa.com.br", false)]
    [InlineData("https://crm.empresa.com.br", true)]
    [InlineData("http://localhost:5173", true)]
    public void A_url_publica_do_crm_e_obrigatoria_e_completa(string? url, bool valida)
    {
        var resultado = new ValidacaoCrmOptions().Validate(null, new CrmOptions { UrlPublica = url });

        Assert.Equal(valida, resultado.Succeeded);
    }

    [Theory]
    [InlineData("https://crm.empresa.com.br")]
    [InlineData("https://crm.empresa.com.br/")]
    public void O_link_da_ficha_nao_duplica_a_barra(string url)
    {
        Assert.Equal("https://crm.empresa.com.br/leads/1024", new CrmOptions { UrlPublica = url }.LinkDoLead(1024));
    }
}
