using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.Features.Funil;
using Crm.Api.Features.Leads;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.Datas;
using Crm.IntegrationTests.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests;

/// <summary>Cadastro, ficha, lista e edição de leads pela tela. A visibilidade está em LeadsVisibilidadeTests.</summary>
public class LeadsTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Palavra única para achar só os leads do teste na lista, que é compartilhada.</summary>
    private static string Marca() => "zq" + Guid.NewGuid().ToString("N")[..10];

    private static async Task<JsonElement> FichaAsync(HttpClient cliente, long id)
    {
        var resposta = await cliente.GetAsync($"/api/leads/{id}", Ct);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return await resposta.LerJsonAsync();
    }

    private static async Task<JsonElement> ListaAsync(HttpClient cliente, string query)
    {
        var resposta = await cliente.GetAsync($"/api/leads?{query}", Ct);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return await resposta.LerJsonAsync();
    }

    private static List<long> Ids(JsonElement lista) =>
        [.. lista.GetProperty("itens").EnumerateArray().Select(l => l.GetProperty("id").GetInt64())];

    private static Task<HttpResponseMessage> EditarAsync(HttpClient cliente, long id, JsonElement ficha, Action<Dictionary<string, object?>> mudar)
    {
        var corpo = new Dictionary<string, object?>
        {
            ["nome"] = ficha.GetProperty("nome").GetString(),
            ["email"] = ficha.GetProperty("email").GetString(),
            ["telefone"] = ficha.GetProperty("telefone").GetString(),
            ["cpfCnpj"] = ficha.GetProperty("cpfCnpj").GetString(),
            ["uf"] = ficha.GetProperty("uf").GetString(),
            ["empresa"] = ficha.GetProperty("empresa").GetString(),
            ["versao"] = ficha.GetProperty("versao").GetUInt32(),
        };
        mudar(corpo);
        return cliente.PutAsJsonAsync($"/api/leads/{id}", corpo, Ct);
    }

    [Fact]
    public async Task Atendente_cadastra_lead_que_fica_com_ela_e_com_os_dados_normalizados()
    {
        var eu = await fabrica.CriarUsuarioAsync();
        using var atendente = await fabrica.LogarAsync(eu);
        var telefone = LeadsDeTeste.TelefoneUnico();
        var cpf = LeadsDeTeste.CpfUnico();
        var corpo = LeadsDeTeste.CorpoLead("  João   da Silva ");
        corpo["email"] = ((string)corpo["email"]!).ToUpperInvariant();
        corpo["telefone"] = LeadsDeTeste.ComoDigitado(telefone);
        corpo["cpfCnpj"] = $"{cpf[..3]}.{cpf[3..6]}.{cpf[6..9]}-{cpf[9..]}";
        corpo["uf"] = "rs";
        corpo["origem"] = "whatsapp";

        var resposta = await atendente.CriarLeadAsync(corpo);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var criado = await resposta.LerJsonAsync();
        var id = criado.GetProperty("leadId").GetInt64();
        Assert.Equal("criado", criado.GetProperty("situacao").GetString());
        Assert.True(criado.GetProperty("podeAbrir").GetBoolean());
        Assert.Equal($"/api/leads/{id}", resposta.Headers.Location?.AbsolutePath);

        var ficha = await FichaAsync(atendente, id);
        Assert.Equal("João da Silva", ficha.GetProperty("nome").GetString());
        Assert.Equal(((string)corpo["email"]!).ToLowerInvariant(), ficha.GetProperty("email").GetString());
        Assert.Equal(telefone, ficha.GetProperty("telefone").GetString());
        Assert.Equal(cpf, ficha.GetProperty("cpfCnpj").GetString());
        Assert.Equal("RS", ficha.GetProperty("uf").GetString());
        Assert.Equal("whatsapp", ficha.GetProperty("origem").GetString());
        Assert.Equal("Novo", ficha.GetProperty("etapa").GetProperty("nome").GetString());
        Assert.Equal(eu.Id, ficha.GetProperty("responsavel").GetProperty("id").GetGuid());
        Assert.Empty(ficha.GetProperty("avisos").EnumerateArray());
    }

    [Theory]
    [InlineData("12ABC34501DE35")]
    [InlineData("11222333000181")]
    [InlineData("52998224725")]
    public async Task Valores_de_teste_da_especificacao_passam_na_validacao(string documento)
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var corpo = LeadsDeTeste.CorpoLead();
        corpo["cpfCnpj"] = documento.ToLowerInvariant();

        var resposta = await admin.CriarLeadAsync(corpo);

        Assert.True(resposta.IsSuccessStatusCode, $"Respondeu {(int)resposta.StatusCode}");
        var ficha = await FichaAsync(admin, (await resposta.LerJsonAsync()).GetProperty("leadId").GetInt64());
        Assert.Equal(documento, ficha.GetProperty("cpfCnpj").GetString());
        Assert.DoesNotContain(
            ficha.GetProperty("avisos").EnumerateArray(), a => a.GetProperty("campo").GetString() == "cpfCnpj");
    }

    [Fact]
    public async Task Na_tela_a_validacao_e_estrita_e_mostra_o_erro_no_campo()
    {
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var corpo = LeadsDeTeste.CorpoLead(" ");
        corpo["email"] = "maria@sem-dominio";
        corpo["telefone"] = "123";
        corpo["cpfCnpj"] = "529.982.247-26";
        corpo["uf"] = "XX";
        corpo["origem"] = "meta_lead_ads";

        var resposta = await atendente.CriarLeadAsync(corpo);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal(["Informe o nome."], await resposta.LerErrosDoCampoAsync("nome"));
        Assert.Equal([RegrasLead.MensagemEmail], await resposta.LerErrosDoCampoAsync("email"));
        Assert.Equal([RegrasLead.MensagemTelefone], await resposta.LerErrosDoCampoAsync("telefone"));
        Assert.Equal([RegrasLead.MensagemDocumento], await resposta.LerErrosDoCampoAsync("cpfCnpj"));
        Assert.Equal([RegrasLead.MensagemUf], await resposta.LerErrosDoCampoAsync("uf"));
        Assert.Equal([RegrasLead.MensagemOrigem], await resposta.LerErrosDoCampoAsync("origem"));
    }

    [Fact]
    public async Task Sem_email_e_sem_telefone_responde_400()
    {
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var corpo = LeadsDeTeste.CorpoLead();
        corpo["email"] = " ";

        var resposta = await atendente.CriarLeadAsync(corpo);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal([RegrasLead.MensagemContato], await resposta.LerErrosDoCampoAsync("email"));
    }

    [Fact]
    public async Task Admin_escolhe_a_responsavel_ou_deixa_sem_responsavel()
    {
        var atendente = await fabrica.CriarUsuarioAsync();
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var paraAtendente = LeadsDeTeste.CorpoLead();
        paraAtendente["responsavelId"] = atendente.Id;

        var comResponsavel = await admin.CriarLeadIdAsync(paraAtendente);
        var semResponsavel = await admin.CriarLeadIdAsync();

        Assert.Equal(atendente.Id, (await FichaAsync(admin, comResponsavel)).GetProperty("responsavel").GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, (await FichaAsync(admin, semResponsavel)).GetProperty("responsavel").ValueKind);
    }

    [Fact]
    public async Task Admin_nao_escolhe_usuario_desativado()
    {
        var desativado = await fabrica.CriarUsuarioAsync(ativo: false);
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var corpo = LeadsDeTeste.CorpoLead();
        corpo["responsavelId"] = desativado.Id;

        var resposta = await admin.CriarLeadAsync(corpo);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal(["Responsável não encontrado ou desativado."], await resposta.LerErrosDoCampoAsync("responsavelId"));
    }

    [Fact]
    public async Task Atendente_nao_escolhe_outra_responsavel()
    {
        var outra = await fabrica.CriarUsuarioAsync();
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var corpo = LeadsDeTeste.CorpoLead();
        corpo["responsavelId"] = outra.Id;

        var resposta = await atendente.CriarLeadAsync(corpo);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal(["Só um admin escolhe a responsável."], await resposta.LerErrosDoCampoAsync("responsavelId"));
    }

    [Fact]
    public async Task Cadastrar_alguem_que_ja_e_lead_de_outra_atendente_vincula_sem_abrir_a_ficha()
    {
        using var adriana = await fabrica.LogarComoAsync(Perfil.Atendente);
        using var pietra = await fabrica.LogarComoAsync(Perfil.Atendente);
        var corpo = LeadsDeTeste.CorpoLead();
        var leadDaAdriana = await adriana.CriarLeadIdAsync(corpo);
        var repetido = LeadsDeTeste.CorpoLead("Outro Nome");
        repetido["email"] = corpo["email"];
        repetido["empresa"] = "Empresa Nova";

        var resposta = await pietra.CriarLeadAsync(repetido);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var resultado = await resposta.LerJsonAsync();
        Assert.Equal(leadDaAdriana, resultado.GetProperty("leadId").GetInt64());
        Assert.Equal("vinculado", resultado.GetProperty("situacao").GetString());
        Assert.False(resultado.GetProperty("podeAbrir").GetBoolean());
        Assert.Equal("Empresa Nova", (await FichaAsync(adriana, leadDaAdriana)).GetProperty("empresa").GetString());
    }

    [Fact]
    public async Task Telefone_invalido_da_integracao_e_aceito_e_a_ficha_mostra_o_aviso()
    {
        using var n8n = fabrica.CriarClienteIntegracao();
        var corpo = LeadsDeTeste.CorpoIntegracao();
        corpo["telefone"] = "(51) 9999-888";
        var resposta = await n8n.EnviarLeadAsync(corpo);
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);

        var ficha = await FichaAsync(admin, (await resposta.LerJsonAsync()).GetProperty("leadId").GetInt64());

        Assert.Equal("(51) 9999-888", ficha.GetProperty("telefone").GetString());
        Assert.Equal(
            [("telefone", AvisosLead.TelefoneNaoReconhecido), ("cpfCnpj", AvisosLead.SemDocumento)],
            ficha.GetProperty("avisos").EnumerateArray().Select(a => (a.GetProperty("campo").GetString(), a.GetProperty("mensagem").GetString())));
    }

    [Fact]
    public async Task Ficha_traz_a_origem_a_campanha_e_as_respostas_do_formulario()
    {
        using var n8n = fabrica.CriarClienteIntegracao();
        var id = await n8n.EnviarLeadIdAsync(LeadsDeTeste.CorpoIntegracao());
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);

        var ficha = await FichaAsync(admin, id);

        Assert.Equal("meta_lead_ads", ficha.GetProperty("origem").GetString());
        Assert.Equal("instagram", ficha.GetProperty("plataforma").GetString());
        Assert.Equal("Outubro | Serviço Y", ficha.GetProperty("campanha").GetProperty("nome").GetString());
        Assert.Equal("outubro-servico-y", ficha.GetProperty("utmCampaign").GetString());
        var resposta = Assert.Single(ficha.GetProperty("respostasExtras").EnumerateArray());
        Assert.Equal("Qual serviço você procura?", resposta.GetProperty("pergunta").GetString());
        Assert.Equal("Serviço Y", resposta.GetProperty("resposta").GetString());
    }

    [Fact]
    public async Task Lead_inexistente_responde_404()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/leads/999999999", Ct)).StatusCode);
    }

    [Fact]
    public async Task Busca_ignora_acentos_e_acha_pelo_telefone_e_pelo_documento()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var marca = Marca();
        var telefone = LeadsDeTeste.TelefoneUnico();
        var cpf = LeadsDeTeste.CpfUnico();
        var corpo = LeadsDeTeste.CorpoLead($"João Ávila {marca}");
        corpo["telefone"] = telefone;
        corpo["cpfCnpj"] = cpf;
        var id = await admin.CriarLeadIdAsync(corpo);
        var outraEmpresa = LeadsDeTeste.CorpoLead("Outra Pessoa");
        outraEmpresa["empresa"] = $"Padaria São Jorge {marca}";
        var daEmpresa = await admin.CriarLeadIdAsync(outraEmpresa);

        Assert.Equal([id], Ids(await ListaAsync(admin, $"busca={Uri.EscapeDataString($"joao avila {marca}")}")));
        Assert.Equal([daEmpresa], Ids(await ListaAsync(admin, $"busca={Uri.EscapeDataString($"sao jorge {marca}")}")));
        Assert.Equal([id], Ids(await ListaAsync(admin, $"busca={Uri.EscapeDataString(LeadsDeTeste.ComoDigitado(telefone))}")));
        Assert.Equal([id], Ids(await ListaAsync(admin, $"busca={cpf[..3]}.{cpf[3..6]}.{cpf[6..9]}-{cpf[9..]}")));
        Assert.Equal([id], Ids(await ListaAsync(admin, $"busca={Uri.EscapeDataString((string)corpo["email"]!)}")));
        Assert.Empty(Ids(await ListaAsync(admin, $"busca={Uri.EscapeDataString($"%{marca}_")}")));
    }

    [Fact]
    public async Task Filtra_por_origem_etapa_campanha_responsavel_e_periodo()
    {
        var atendente = await fabrica.CriarUsuarioAsync();
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        using var n8n = fabrica.CriarClienteIntegracao();
        var marca = Marca();
        var daMeta = LeadsDeTeste.CorpoIntegracao();
        daMeta["nome"] = $"Da Meta {marca}";
        var idMeta = await n8n.EnviarLeadIdAsync(daMeta);
        var manual = LeadsDeTeste.CorpoLead($"Manual {marca}");
        manual["responsavelId"] = atendente.Id;
        var idManual = await admin.CriarLeadIdAsync(manual);
        await fabrica.MoverParaAsync(idManual, TipoEtapa.Perdido);
        var perdido = await fabrica.BuscarLeadAsync(idManual);
        var campanhaId = (await fabrica.BuscarLeadAsync(idMeta)).CampanhaId;
        var hoje = HorarioBrasilia.Dia(DateTimeOffset.UtcNow);
        var busca = $"busca={marca}";

        Assert.Equal([idMeta], Ids(await ListaAsync(admin, $"{busca}&origem=meta_lead_ads")));
        Assert.Equal([idManual], Ids(await ListaAsync(admin, $"{busca}&etapaId={perdido.EtapaId}")));
        Assert.Equal([idMeta], Ids(await ListaAsync(admin, $"{busca}&campanhaId={campanhaId}")));
        Assert.Equal([idManual], Ids(await ListaAsync(admin, $"{busca}&responsavelId={atendente.Id}")));
        Assert.Equal([idMeta], Ids(await ListaAsync(admin, $"{busca}&semResponsavel=true")));
        Assert.Equal(2, Ids(await ListaAsync(admin, $"{busca}&de={hoje:yyyy-MM-dd}&ate={hoje:yyyy-MM-dd}")).Count);
        Assert.Empty(Ids(await ListaAsync(admin, $"{busca}&de={hoje.AddDays(1):yyyy-MM-dd}")));
        Assert.Empty(Ids(await ListaAsync(admin, $"{busca}&ate={hoje.AddDays(-1):yyyy-MM-dd}")));
    }

    [Fact]
    public async Task Pagina_e_ordena()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var marca = Marca();
        var carla = await admin.CriarLeadIdAsync(LeadsDeTeste.CorpoLead($"Carla {marca}"));
        var ana = await admin.CriarLeadIdAsync(LeadsDeTeste.CorpoLead($"Ana {marca}"));
        var bia = await admin.CriarLeadIdAsync(LeadsDeTeste.CorpoLead($"Bia {marca}"));

        var recentes = await ListaAsync(admin, $"busca={marca}&tamanho=2");
        var segundaPagina = await ListaAsync(admin, $"busca={marca}&tamanho=2&pagina=2");

        Assert.Equal(3, recentes.GetProperty("total").GetInt32());
        Assert.Equal([bia, ana], Ids(recentes));
        Assert.Equal([carla], Ids(segundaPagina));
        Assert.Equal([carla, ana, bia], Ids(await ListaAsync(admin, $"busca={marca}&ordem=Antigos")));
        Assert.Equal([ana, bia, carla], Ids(await ListaAsync(admin, $"busca={marca}&ordem=Nome")));
    }

    [Theory]
    [InlineData("tamanho=0", "tamanho")]
    [InlineData("tamanho=101", "tamanho")]
    [InlineData("pagina=0", "pagina")]
    [InlineData("de=2026-10-10&ate=2026-10-01", "ate")]
    public async Task Parametros_invalidos_da_lista_respondem_400(string query, string campo)
    {
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);

        var resposta = await atendente.GetAsync($"/api/leads?{query}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.NotEmpty(await resposta.LerErrosDoCampoAsync(campo));
    }

    [Fact]
    public async Task Edita_os_dados_e_a_auditoria_guarda_so_os_nomes_dos_campos()
    {
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var id = await atendente.CriarLeadIdAsync();
        var ficha = await FichaAsync(atendente, id);

        var resposta = await EditarAsync(atendente, id, ficha, corpo =>
        {
            corpo["empresa"] = "Empresa Editada";
            corpo["uf"] = "sc";
        });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var editada = await resposta.LerJsonAsync();
        Assert.Equal("Empresa Editada", editada.GetProperty("empresa").GetString());
        Assert.Equal("SC", editada.GetProperty("uf").GetString());
        Assert.NotEqual(ficha.GetProperty("versao").GetUInt32(), editada.GetProperty("versao").GetUInt32());

        await using var escopo = fabrica.Services.CreateAsyncScope();
        var registro = await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().Auditoria
            .SingleAsync(r => r.Acao == AcoesAuditoria.LeadEditado && r.EntidadeId == id.ToString(), Ct);
        Assert.Equal("""["uf","empresa"]""", registro.Detalhes!.RootElement.GetProperty("campos").GetRawText().Replace(" ", ""));
        Assert.DoesNotContain("Editada", registro.Detalhes.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Versao_desatualizada_responde_409_e_nao_sobrescreve()
    {
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var id = await atendente.CriarLeadIdAsync();
        var fichaAntiga = await FichaAsync(atendente, id);
        await EditarAsync(atendente, id, fichaAntiga, corpo => corpo["empresa"] = "Primeira edição");

        var resposta = await EditarAsync(atendente, id, fichaAntiga, corpo => corpo["empresa"] = "Segunda edição");

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
        Assert.Equal(LeadsController.MensagemConflito, await resposta.LerDetalheAsync());
        Assert.Equal("Primeira edição", (await fabrica.BuscarLeadAsync(id)).Empresa);
    }

    [Fact]
    public async Task Dado_invalido_que_veio_da_integracao_nao_impede_salvar_mas_um_novo_invalido_sim()
    {
        using var n8n = fabrica.CriarClienteIntegracao();
        var corpo = LeadsDeTeste.CorpoIntegracao();
        corpo["telefone"] = "123";
        var id = await n8n.EnviarLeadIdAsync(corpo);
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var ficha = await FichaAsync(admin, id);

        var mantendo = await EditarAsync(admin, id, ficha, c => c["empresa"] = "Outra Empresa");
        var trocando = await EditarAsync(admin, id, await FichaAsync(admin, id), c => c["telefone"] = "456");

        Assert.Equal(HttpStatusCode.OK, mantendo.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, trocando.StatusCode);
        Assert.Equal([RegrasLead.MensagemTelefone], await trocando.LerErrosDoCampoAsync("telefone"));
        Assert.Equal("123", (await fabrica.BuscarLeadAsync(id)).Telefone);
    }

    [Fact]
    public async Task Atendente_ve_as_campanhas_para_filtrar()
    {
        using var n8n = fabrica.CriarClienteIntegracao();
        var id = await n8n.EnviarLeadIdAsync(LeadsDeTeste.CorpoIntegracao());
        var campanhaId = (await fabrica.BuscarLeadAsync(id)).CampanhaId;
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);

        var resposta = await atendente.GetAsync("/api/campanhas", Ct);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var campanha = (await resposta.LerJsonAsync()).EnumerateArray().Single(c => c.GetProperty("id").GetInt32() == campanhaId);
        Assert.Equal("meta_lead_ads", campanha.GetProperty("origem").GetString());
    }
}
