using System.Net;
using System.Net.Http.Json;
using Crm.Api.Features.Funil;
using Crm.Api.Features.Leads;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Dados;
using Crm.IntegrationTests.Infra;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests;

/// <summary><c>POST /api/integracoes/leads</c> (seção 4.2), como o n8n chama.</summary>
public class IntegracaoLeadsTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(long LeadId, string? Situacao)> LerResultadoAsync(HttpResponseMessage resposta)
    {
        var corpo = await resposta.LerJsonAsync();
        return (corpo.GetProperty("leadId").GetInt64(), corpo.GetProperty("situacao").GetString());
    }

    private async Task<int> ContarLeadsComEmailAsync(string email)
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().Leads.CountAsync(l => l.Email == email, Ct);
    }

    [Fact]
    public async Task Lead_novo_responde_201_e_guarda_o_payload_bruto()
    {
        using var n8n = fabrica.CriarClienteIntegracao();
        var corpo = LeadsDeTeste.CorpoIntegracao();
        corpo["campoQueOCrmNaoConhece"] = "fica no payload bruto";

        var resposta = await n8n.EnviarLeadAsync(corpo);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var (leadId, situacao) = await LerResultadoAsync(resposta);
        Assert.Equal("criado", situacao);
        var lead = await fabrica.BuscarLeadAsync(leadId);
        Assert.Equal(Origem.MetaLeadAds, lead.Origem);
        Assert.Equal("Outubro | Serviço Y", lead.Campanha!.Nome);
        Assert.Equal("Novo", lead.Etapa.Nome);

        await using var escopo = fabrica.Services.CreateAsyncScope();
        var entrada = await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().EntradasRecebidas.SingleAsync(e => e.LeadId == leadId, Ct);
        Assert.Equal(new DateTimeOffset(2026, 10, 5, 14, 3, 0, TimeSpan.Zero), entrada.RecebidoEm);
        Assert.Equal("fica no payload bruto", entrada.PayloadBruto!.RootElement.GetProperty("campoQueOCrmNaoConhece").GetString());
        Assert.Equal("Vídeo 1", entrada.Anuncio!.RootElement.GetProperty("nome").GetString());
    }

    [Fact]
    public async Task O_mesmo_id_externo_enviado_duas_vezes_gera_um_lead_so()
    {
        using var n8n = fabrica.CriarClienteIntegracao();
        var corpo = LeadsDeTeste.CorpoIntegracao();

        var primeira = await n8n.EnviarLeadAsync(corpo);
        var segunda = await n8n.EnviarLeadAsync(corpo);

        Assert.Equal(HttpStatusCode.Created, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
        var (leadId, _) = await LerResultadoAsync(primeira);
        Assert.Equal((leadId, "ja_recebido"), await LerResultadoAsync(segunda));
        Assert.Equal(1, await ContarLeadsComEmailAsync((string)corpo["email"]!));
    }

    [Fact]
    public async Task O_mesmo_email_de_outra_origem_vincula_e_reabre_o_lead_perdido()
    {
        using var n8n = fabrica.CriarClienteIntegracao();
        var meta = LeadsDeTeste.CorpoIntegracao();
        var (leadId, _) = await LerResultadoAsync(await n8n.EnviarLeadAsync(meta));
        await fabrica.MoverParaAsync(leadId, TipoEtapa.Perdido);
        var google = LeadsDeTeste.CorpoIntegracao("google_ads");
        google["email"] = ((string)meta["email"]!).ToUpperInvariant();
        google["campanha"] = new { id = LeadsDeTeste.IdExternoUnico() };

        var resposta = await n8n.EnviarLeadAsync(google);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal((leadId, "vinculado"), await LerResultadoAsync(resposta));
        var lead = await fabrica.BuscarLeadAsync(leadId);
        Assert.Equal("Novo", lead.Etapa.Nome);
        Assert.Null(lead.MotivoPerdaId);
        Assert.Equal(Origem.MetaLeadAds, lead.Origem);
        Assert.Equal(1, await ContarLeadsComEmailAsync((string)meta["email"]!));
    }

    [Fact]
    public async Task Telefone_email_e_documento_invalidos_nao_recusam_o_lead()
    {
        using var n8n = fabrica.CriarClienteIntegracao();
        var corpo = LeadsDeTeste.CorpoIntegracao();
        corpo["telefone"] = "(51) 9999-888";
        corpo["email"] = "maria@sem-dominio";
        corpo["cpfCnpj"] = "123.456.789-00";
        corpo["uf"] = "Terra do Nunca";

        var resposta = await n8n.EnviarLeadAsync(corpo);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var lead = await fabrica.BuscarLeadAsync((await LerResultadoAsync(resposta)).LeadId);
        Assert.Equal("(51) 9999-888", lead.Telefone);
        Assert.Equal("maria@sem-dominio", lead.Email);
        Assert.Equal("12345678900", lead.CpfCnpj);
        Assert.Null(lead.Uf);
    }

    [Fact]
    public async Task Email_com_acento_entra_como_veio_com_o_aviso_na_ficha()
    {
        using var n8n = fabrica.CriarClienteIntegracao();
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var corpo = LeadsDeTeste.CorpoIntegracao();
        var email = $"notificação-{Guid.NewGuid():N}@exemplo.com";
        corpo["email"] = email;

        var resposta = await n8n.EnviarLeadAsync(corpo);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var ficha = await (await admin.GetAsync($"/api/leads/{(await LerResultadoAsync(resposta)).LeadId}", Ct)).LerJsonAsync();
        Assert.Equal(email, ficha.GetProperty("email").GetString());
        Assert.Contains(
            ficha.GetProperty("avisos").EnumerateArray(),
            aviso => aviso.GetProperty("campo").GetString() == "email"
                && aviso.GetProperty("mensagem").GetString() == AvisosLead.EmailInvalido);
    }

    [Fact]
    public async Task So_os_campos_obrigatorios_bastam()
    {
        using var n8n = fabrica.CriarClienteIntegracao();

        var resposta = await n8n.EnviarLeadAsync(new
        {
            origem = "google_ads",
            idExterno = LeadsDeTeste.IdExternoUnico(),
            recebidoEm = "2026-10-05T11:03:00-03:00",
            teste = (bool?)null,
            nome = "Maria Souza",
            telefone = LeadsDeTeste.TelefoneUnico(),
            campanha = new { id = LeadsDeTeste.IdExternoUnico() },
        });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var lead = await fabrica.BuscarLeadAsync((await LerResultadoAsync(resposta)).LeadId);
        Assert.False(lead.Teste);
        Assert.StartsWith("Google Ads · campanha ", lead.Campanha!.Nome);
    }

    [Fact]
    public async Task Lead_de_teste_fica_marcado_e_sem_responsavel()
    {
        using var n8n = fabrica.CriarClienteIntegracao();
        var corpo = LeadsDeTeste.CorpoIntegracao();
        corpo["teste"] = true;

        var resposta = await n8n.EnviarLeadAsync(corpo);

        var lead = await fabrica.BuscarLeadAsync((await LerResultadoAsync(resposta)).LeadId);
        Assert.True(lead.Teste);
        Assert.Null(lead.ResponsavelId);
    }

    [Theory]
    [InlineData("nome", null, "nome", "Informe o nome.")]
    [InlineData("nome", "   ", "nome", "Informe o nome.")]
    [InlineData("idExterno", null, "idExterno", "Informe o idExterno.")]
    [InlineData("recebidoEm", null, "recebidoEm", "Informe o recebidoEm, em ISO 8601.")]
    [InlineData("origem", "whatsapp", "origem", "Origem inválida. Use meta_lead_ads, google_ads, site.")]
    [InlineData("origem", null, "origem", "Origem inválida. Use meta_lead_ads, google_ads, site.")]
    public async Task Problema_de_estrutura_responde_400_com_a_mensagem_do_campo(string campo, string? valor, string campoDoErro, string mensagem)
    {
        using var n8n = fabrica.CriarClienteIntegracao();
        var corpo = LeadsDeTeste.CorpoIntegracao();
        corpo[campo] = valor;

        var resposta = await n8n.EnviarLeadAsync(corpo);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal([mensagem], await resposta.LerErrosDoCampoAsync(campoDoErro));
        Assert.Equal(0, await ContarLeadsComEmailAsync((string)corpo["email"]!));
    }

    [Fact]
    public async Task Sem_email_e_sem_telefone_responde_400()
    {
        using var n8n = fabrica.CriarClienteIntegracao();
        var corpo = LeadsDeTeste.CorpoIntegracao();
        corpo.Remove("email");
        corpo["telefone"] = " ";

        var resposta = await n8n.EnviarLeadAsync(corpo);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal(["Informe o e-mail ou o telefone."], await resposta.LerErrosDoCampoAsync("email"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("chave-errada-mas-com-o-mesmo-tamanho-0123")]
    [InlineData("")]
    public async Task Sem_a_chave_certa_responde_401(string? chave)
    {
        using var n8n = fabrica.CriarClienteIntegracao(chave);

        var resposta = await n8n.EnviarLeadAsync(LeadsDeTeste.CorpoIntegracao());

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Sessao_de_admin_nao_vale_na_integracao()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);

        var resposta = await admin.EnviarLeadAsync(LeadsDeTeste.CorpoIntegracao());

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Sem_chave_configurada_a_integracao_fica_desligada()
    {
        using var api = fabrica.WithWebHostBuilder(builder => builder.UseSetting("Integracao:Chave", ""));
        using var n8n = api.CriarClienteIntegracao();

        var resposta = await n8n.EnviarLeadAsync(LeadsDeTeste.CorpoIntegracao());

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public void Chave_curta_impede_a_api_de_subir()
    {
        using var api = fabrica.WithWebHostBuilder(builder => builder.UseSetting("Integracao:Chave", "curta"));

        var erro = Assert.ThrowsAny<Exception>(() => api.CreateClient());

        Assert.Contains("Integracao__Chave precisa ter pelo menos 32 caracteres", erro.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Passar_do_limite_por_minuto_responde_429()
    {
        // Fábrica à parte, com um limite baixo e um contador só dela.
        using var api = fabrica.WithWebHostBuilder(builder => builder.UseSetting("Integracao:RequisicoesPorMinuto", "2"));
        using var n8n = api.CriarClienteIntegracao();

        await n8n.EnviarLeadAsync(LeadsDeTeste.CorpoIntegracao());
        await n8n.EnviarLeadAsync(LeadsDeTeste.CorpoIntegracao());
        var terceira = await n8n.EnviarLeadAsync(LeadsDeTeste.CorpoIntegracao());

        Assert.Equal(HttpStatusCode.TooManyRequests, terceira.StatusCode);
        Assert.True(terceira.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task Json_malformado_responde_400()
    {
        using var n8n = fabrica.CriarClienteIntegracao();

        var resposta = await n8n.PostAsync(
            "/api/integracoes/leads", new StringContent("{ nao é json", System.Text.Encoding.UTF8, "application/json"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }
}
