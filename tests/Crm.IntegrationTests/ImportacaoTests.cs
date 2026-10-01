using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Crm.Api.Features.Funil;
using Crm.Api.Features.Leads;
using Crm.Api.Features.Notificacoes;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.Importacoes;
using Crm.IntegrationTests.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests;

/// <summary>Importação de CSV (seção 3.7). O rodízio fica em ImportacaoRodizioTests, e a retomada, em ImportacaoRetomadaTests.</summary>
public class ImportacaoTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string[] Campos(JsonElement analise) =>
        [.. analise.GetProperty("colunas").EnumerateArray().Select(c => c.GetProperty("sugestao").GetString()!)];

    [Fact]
    public async Task Csv_da_Meta_em_UTF16_com_tab_entra_com_os_acentos_certos()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var atendente = await fabrica.CriarUsuarioAsync();
        var idLead = ImportacoesDeTeste.IdDaMeta();
        var campanhaId = ImportacoesDeTeste.IdDaMeta();
        var telefone = LeadsDeTeste.TelefoneUnico();
        var arquivo = ImportacoesDeTeste.ArquivoDaMeta(
            ImportacoesDeTeste.LinhaMeta("João Conceição", idLead, telefone: telefone, campanhaId: campanhaId, resposta: "Instalação e manutenção"),
            ImportacoesDeTeste.LinhaMeta("Ana Lúcia Gonçalves", campanhaId: campanhaId, empresa: "Pão de Açúcar – Centro"));

        var analise = await admin.AnalisarAsync(arquivo, "leads-meta.csv");
        var resultado = await admin.ImportarAsync(arquivo, corpo => corpo["responsavelId"] = atendente.Id);

        Assert.Equal("utf-16", analise.GetProperty("codificacao").GetString());
        Assert.Equal("tab", analise.GetProperty("separador").GetString());
        Assert.Equal(2, analise.GetProperty("totalLinhas").GetInt32());
        Assert.Equal(0, analise.GetProperty("colunaIdExterno").GetInt32());
        Assert.Equal("meta_lead_ads", analise.GetProperty("origem").GetString());
        Assert.Equal("João Conceição", analise.GetProperty("previa")[0][12].GetString());
        Assert.Equal("qual_serviço_você_procura?", analise.GetProperty("colunas")[16].GetProperty("nome").GetString());

        Assert.Equal(2, resultado.GetProperty("criados").GetInt32());
        Assert.Equal(0, resultado.GetProperty("comErro").GetInt32());
        var leads = await fabrica.LeadsDaImportacaoAsync(resultado.GetProperty("id").GetInt64());
        Assert.Equal(["João Conceição", "Ana Lúcia Gonçalves"], leads.Select(l => l.Nome));
        Assert.Equal(["Açaí & Cia", "Pão de Açúcar – Centro"], leads.Select(l => l.Empresa));
        var joao = leads[0];
        Assert.Equal(telefone, joao.Telefone);
        Assert.Equal(Origem.MetaLeadAds, joao.Origem);
        Assert.Equal("instagram", joao.Plataforma);
        Assert.Equal(campanhaId, joao.Campanha!.IdExterno);
        Assert.Equal("Outubro | Serviço Y", joao.Campanha.Nome);
        Assert.Equal(atendente.Id, joao.ResponsavelId);
        Assert.Equal("Instalação e manutenção", joao.RespostasExtras!.RootElement.GetProperty("qual_serviço_você_procura?").GetString());
        var entrada = Assert.Single(await fabrica.EntradasDaImportacaoAsync(resultado.GetProperty("id").GetInt64()), e => e.LeadId == joao.Id);
        Assert.Equal(idLead, entrada.IdExterno);
        Assert.Equal("987654321", entrada.Formulario!.RootElement.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Csv_do_Excel_com_ponto_e_virgula_em_Windows1252_entra_com_os_acentos_certos()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var campanha = $"Feira de Gramado – {Guid.NewGuid():N}"[..40];
        var arquivo = ImportacoesDeTeste.ArquivoDoExcel(
            ImportacoesDeTeste.LinhaExcel("Maria da Conceição", empresa: "Construções São João", observacao: "Ligar à tarde; prefere WhatsApp"),
            ImportacoesDeTeste.LinhaExcel("Antônio Gonçalves", empresa: "Açougue Pão & Cia"));

        var analise = await admin.AnalisarAsync(arquivo, "planilha.csv");
        var resultado = await admin.ImportarAsync(arquivo, corpo => corpo["novaCampanha"] = campanha);

        Assert.Equal("windows-1252", analise.GetProperty("codificacao").GetString());
        Assert.Equal("ponto_e_virgula", analise.GetProperty("separador").GetString());
        Assert.Equal("planilha", analise.GetProperty("origem").GetString());
        Assert.Equal(["nome", "email", "telefone", "empresa", "resposta_extra"], Campos(analise));

        Assert.Equal(2, resultado.GetProperty("criados").GetInt32());
        Assert.Equal(campanha, resultado.GetProperty("campanhaPadrao").GetString());
        var leads = await fabrica.LeadsDaImportacaoAsync(resultado.GetProperty("id").GetInt64());
        Assert.Equal(["Maria da Conceição", "Antônio Gonçalves"], leads.Select(l => l.Nome));
        Assert.Equal(["Construções São João", "Açougue Pão & Cia"], leads.Select(l => l.Empresa));
        Assert.All(leads, l => Assert.Equal(Origem.Planilha, l.Origem));
        Assert.All(leads, l => Assert.Equal(campanha, l.Campanha!.Nome));
        Assert.Equal("Ligar à tarde; prefere WhatsApp", leads[0].RespostasExtras!.RootElement.GetProperty("Observações").GetString());
        Assert.StartsWith("+55", leads[0].Telefone, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reimportar_o_mesmo_arquivo_nao_cria_duplicados()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var emailMeta = LeadsDeTeste.EmailUnico();
        var emailExcel = LeadsDeTeste.EmailUnico();

        // Com a coluna de id (Meta) e sem ela (Excel, pelo hash da linha). O telefone inválido do Excel não serve para
        // achar a pessoa: só o id evita o lead repetido.
        var meta = ImportacoesDeTeste.ArquivoDaMeta(ImportacoesDeTeste.LinhaMeta("Maria Souza", email: emailMeta), ImportacoesDeTeste.LinhaMeta("João Silva"));
        var excel = ImportacoesDeTeste.ArquivoDoExcel(
            ImportacoesDeTeste.LinhaExcel("Ana Souza", email: emailExcel),
            ImportacoesDeTeste.LinhaExcel("Pedro Sem Email", email: "", celular: "123"));

        var metaPrimeira = await admin.ImportarAsync(meta);
        var metaSegunda = await admin.ImportarAsync(meta);
        var excelPrimeira = await admin.ImportarAsync(excel);
        var excelSegunda = await admin.ImportarAsync(excel);

        foreach (var (primeira, segunda) in new[] { (metaPrimeira, metaSegunda), (excelPrimeira, excelSegunda) })
        {
            Assert.Equal(2, primeira.GetProperty("criados").GetInt32());
            Assert.Equal(0, segunda.GetProperty("criados").GetInt32());
            Assert.Equal(0, segunda.GetProperty("vinculados").GetInt32());
            Assert.Equal(2, segunda.GetProperty("jaImportados").GetInt32());
            Assert.Empty(await fabrica.EntradasDaImportacaoAsync(segunda.GetProperty("id").GetInt64()));
        }

        Assert.Single(await fabrica.LeadsComEmailAsync(emailMeta));
        Assert.Single(await fabrica.LeadsComEmailAsync(emailExcel));
    }

    [Fact]
    public async Task Linhas_com_erro_ficam_no_resultado_e_no_relatorio_com_a_linha_e_o_motivo()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var texto = new StringBuilder("Nome;E-mail;Celular\r\n")
            .Append($"Maria Souza;{LeadsDeTeste.EmailUnico()};\r\n")
            .Append($";{LeadsDeTeste.EmailUnico()};\r\n")
            .Append("João Sem Contato;;\r\n")
            .Append("\r\n")
            .Append($"Pedro;{LeadsDeTeste.EmailUnico()};;sobrando\r\n")
            .Append($"Telefone Inválido;;123\r\n")
            .ToString();

        var resultado = await admin.ImportarAsync(ImportacoesDeTeste.Windows1252.GetBytes(texto));
        var id = resultado.GetProperty("id").GetInt64();
        var relatorio = await admin.GetAsync($"/api/importacoes/{id}/erros", Ct);

        Assert.Equal(5, resultado.GetProperty("totalLinhas").GetInt32());
        Assert.Equal(2, resultado.GetProperty("criados").GetInt32());
        Assert.Equal(3, resultado.GetProperty("comErro").GetInt32());
        Assert.Equal(
            [(3, LinhaImportada.ErroSemNome), (4, LinhaImportada.ErroSemContato), (6, ArquivoCsv.ErroColunasAMais)],
            resultado.GetProperty("erros").EnumerateArray().Select(e => (e.GetProperty("linha").GetInt32(), e.GetProperty("motivo").GetString()!)));

        Assert.Equal(HttpStatusCode.OK, relatorio.StatusCode);
        Assert.Equal("text/csv", relatorio.Content.Headers.ContentType?.MediaType);
        Assert.Equal($"importacao-{id}-erros.csv", relatorio.Content.Headers.ContentDisposition?.FileName);
        var bytes = await relatorio.Content.ReadAsByteArrayAsync(Ct);
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        Assert.Equal(
            $"Linha;Motivo\r\n3;{LinhaImportada.ErroSemNome}\r\n4;{LinhaImportada.ErroSemContato}\r\n6;{ArquivoCsv.ErroColunasAMais}\r\n",
            Encoding.UTF8.GetString(bytes[3..]));

        // Formato inválido não é erro: o lead entra, e a ficha avisa.
        var telefoneInvalido = Assert.Single(await fabrica.LeadsDaImportacaoAsync(id), l => l.Nome == "Telefone Inválido");
        var ficha = await (await admin.GetAsync($"/api/leads/{telefoneInvalido.Id}", Ct)).LerJsonAsync();
        Assert.Contains(ficha.GetProperty("avisos").EnumerateArray(), a => a.GetProperty("mensagem").GetString() == AvisosLead.TelefoneNaoReconhecido);
    }

    [Fact]
    public async Task Importacao_nao_avisa_lead_a_lead_nem_grava_evento()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var atendente = await fabrica.CriarUsuarioAsync();
        var existente = await fabrica.ReceberAsync(LeadsDeTeste.Entrada() with { ResponsavelId = atendente.Id, Avisar = false });
        var email = (await fabrica.BuscarLeadAsync(existente.LeadId)).Email!;
        var arquivo = ImportacoesDeTeste.ArquivoDoExcel(ImportacoesDeTeste.LinhaExcel("Maria Nova"), ImportacoesDeTeste.LinhaExcel("Já Existe", email: email));

        var resultado = await admin.ImportarAsync(arquivo, corpo => corpo["responsavelId"] = atendente.Id);

        Assert.Equal(1, resultado.GetProperty("criados").GetInt32());
        Assert.Equal(1, resultado.GetProperty("vinculados").GetInt32());
        foreach (var lead in await fabrica.LeadsDaImportacaoAsync(resultado.GetProperty("id").GetInt64()))
        {
            Assert.Empty(await fabrica.EventosDoLeadAsync(lead.Id));
            Assert.Empty(await fabrica.NotificacoesDoUsuarioAsync(atendente.Id, lead.Id));
        }
    }

    [Fact]
    public async Task No_fim_quem_importou_recebe_o_resumo_e_cada_responsavel_quantos_leads_ganhou()
    {
        var eu = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        var outroAdmin = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        var atendenteUsuario = await fabrica.CriarUsuarioAsync();
        using var admin = await fabrica.LogarAsync(eu);
        using var atendente = await fabrica.LogarAsync(atendenteUsuario);
        var existente = await fabrica.ReceberAsync(LeadsDeTeste.Entrada() with { Avisar = false });
        var email = (await fabrica.BuscarLeadAsync(existente.LeadId)).Email;
        var arquivo = ImportacoesDeTeste.ArquivoDoExcel(
            ImportacoesDeTeste.LinhaExcel("Lead Um"),
            ImportacoesDeTeste.LinhaExcel("Lead Dois"),
            ImportacoesDeTeste.LinhaExcel("Já Existe", email: email),
            ImportacoesDeTeste.LinhaExcel(string.Empty));

        var resultado = await admin.ImportarAsync(arquivo, corpo => corpo["responsavelId"] = atendenteUsuario.Id);
        var id = resultado.GetProperty("id").GetInt64();

        var resumo = Assert.Single(await fabrica.NotificacoesDoUsuarioAsync(eu.Id), n => n.ImportacaoId == id);
        Assert.Equal(TipoNotificacao.ImportacaoConcluida, resumo.Tipo);
        Assert.Equal("Importação concluída: 2 leads novos, 1 vinculado, 0 já importados e 1 com erro", resumo.Titulo);
        Assert.Null(resumo.LeadId);
        var ganhos = Assert.Single(await fabrica.NotificacoesDoUsuarioAsync(atendenteUsuario.Id));
        Assert.Equal(TipoNotificacao.LeadsImportados, ganhos.Tipo);
        Assert.Equal("Você recebeu 2 leads da importação", ganhos.Titulo);
        Assert.Null(ganhos.LeadId);
        Assert.Null(ganhos.ImportacaoId);
        Assert.DoesNotContain(await fabrica.NotificacoesDoUsuarioAsync(outroAdmin.Id), n => n.ImportacaoId == id);

        // Na API, o resumo abre a importação; a notificação da atendente não leva a nenhuma tela de admin.
        Assert.Contains(
            (await admin.NotificacoesAsync()).Itens(),
            n => n.GetProperty("importacaoId").ValueKind == JsonValueKind.Number && n.GetProperty("importacaoId").GetInt64() == id);
        var daAtendente = Assert.Single((await atendente.NotificacoesAsync()).Itens());
        Assert.Equal(JsonValueKind.Null, daAtendente.GetProperty("importacaoId").ValueKind);
        Assert.Equal(JsonValueKind.Null, daAtendente.GetProperty("lead").ValueKind);
    }

    [Fact]
    public async Task Admin_que_importa_para_si_recebe_so_o_resumo()
    {
        var eu = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        using var admin = await fabrica.LogarAsync(eu);

        var resultado = await admin.ImportarAsync(
            ImportacoesDeTeste.ArquivoDoExcel(ImportacoesDeTeste.LinhaExcel("Lead Um")), corpo => corpo["responsavelId"] = eu.Id);

        var daImportacao = (await fabrica.NotificacoesDoUsuarioAsync(eu.Id))
            .Where(n => n.Tipo is TipoNotificacao.ImportacaoConcluida or TipoNotificacao.LeadsImportados);
        var resumo = Assert.Single(daImportacao);
        Assert.Equal(resultado.GetProperty("id").GetInt64(), resumo.ImportacaoId);
        Assert.Equal("Importação concluída: 1 lead novo, 0 vinculados, 0 já importados e 0 com erro", resumo.Titulo);
    }

    [Fact]
    public async Task Pessoa_que_ja_e_lead_e_vinculada_e_o_lead_perdido_volta_para_a_etapa_inicial_com_historico()
    {
        var eu = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        using var admin = await fabrica.LogarAsync(eu);
        var existente = await fabrica.ReceberAsync(LeadsDeTeste.Entrada() with { Avisar = false });
        await fabrica.MoverParaAsync(existente.LeadId, TipoEtapa.Perdido);
        var lead = await fabrica.BuscarLeadAsync(existente.LeadId);
        var arquivo = ImportacoesDeTeste.ArquivoDoExcel(ImportacoesDeTeste.LinhaExcel("Maria Souza", email: lead.Email, empresa: "Empresa Nova"));

        var resultado = await admin.ImportarAsync(arquivo);

        Assert.Equal(1, resultado.GetProperty("vinculados").GetInt32());
        var reaberto = await fabrica.BuscarLeadAsync(existente.LeadId);
        Assert.Equal("Novo", reaberto.Etapa.Nome);
        Assert.Equal("Empresa Nova", reaberto.Empresa);
        var reabertura = (await fabrica.HistoricoAsync(existente.LeadId)).Last();
        Assert.Equal(eu.Id, reabertura.UsuarioId);
    }

    [Fact]
    public async Task Campanha_padrao_vale_para_as_linhas_sem_campanha_e_precisa_ser_da_mesma_origem()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        using var n8n = fabrica.CriarClienteIntegracao();
        var corpoIntegracao = LeadsDeTeste.CorpoIntegracao();
        var leadDaMeta = await fabrica.BuscarLeadAsync(await n8n.EnviarLeadIdAsync(corpoIntegracao));
        var campanhaDaMeta = leadDaMeta.Campanha!;
        var arquivo = ImportacoesDeTeste.ArquivoDoExcel(ImportacoesDeTeste.LinhaExcel("Maria Souza"));

        var outraOrigem = await admin.PostImportacaoAsync(Corpo(arquivo, await admin.AnalisarAsync(arquivo), c => c["campanhaPadraoId"] = campanhaDaMeta.Id));
        var resultado = await admin.ImportarAsync(arquivo, c =>
        {
            c["origem"] = "meta_lead_ads";
            c["campanhaPadraoId"] = campanhaDaMeta.Id;
        });

        Assert.Equal(HttpStatusCode.BadRequest, outraOrigem.StatusCode);
        Assert.Equal([ImportacoesController.MensagemCampanha], await outraOrigem.LerErrosDoCampoAsync("campanhaPadraoId"));
        Assert.Equal(campanhaDaMeta.Nome, resultado.GetProperty("campanhaPadrao").GetString());
        var importado = Assert.Single(await fabrica.LeadsDaImportacaoAsync(resultado.GetProperty("id").GetInt64()));
        Assert.Equal(campanhaDaMeta.Id, importado.CampanhaId);
        Assert.Equal(Origem.MetaLeadAds, importado.Origem);
    }

    [Fact]
    public async Task Responsavel_escolhida_fica_com_os_leads_sem_mexer_na_vez_do_rodizio()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var atendente = await fabrica.CriarUsuarioAsync();
        var arquivo = ImportacoesDeTeste.ArquivoDoExcel(ImportacoesDeTeste.LinhaExcel("Maria Souza"), ImportacoesDeTeste.LinhaExcel("João Silva"));

        var resultado = await admin.ImportarAsync(arquivo, corpo => corpo["responsavelId"] = atendente.Id);

        Assert.Equal(atendente.Id, resultado.GetProperty("responsavel").GetProperty("id").GetGuid());
        Assert.All(await fabrica.LeadsDaImportacaoAsync(resultado.GetProperty("id").GetInt64()), l => Assert.Equal(atendente.Id, l.ResponsavelId));
        Assert.Null((await fabrica.BuscarUsuarioAsync(atendente.Id)).UltimoLeadAtribuidoEm);
        Assert.Equal(0, resultado.GetProperty("semResponsavel").GetInt32());
    }

    [Fact]
    public async Task Leads_importados_para_uma_atendente_nao_aparecem_para_outra()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var adriana = await fabrica.CriarUsuarioAsync();
        using var pietra = await fabrica.LogarComoAsync(Perfil.Atendente);
        var arquivo = ImportacoesDeTeste.ArquivoDoExcel(ImportacoesDeTeste.LinhaExcel("Lead da Adriana"));

        var resultado = await admin.ImportarAsync(arquivo, corpo => corpo["responsavelId"] = adriana.Id);
        var lead = Assert.Single(await fabrica.LeadsDaImportacaoAsync(resultado.GetProperty("id").GetInt64()));

        Assert.Equal(HttpStatusCode.NotFound, (await pietra.GetAsync($"/api/leads/{lead.Id}", Ct)).StatusCode);
        var lista = await (await pietra.GetAsync("/api/leads?tamanho=100", Ct)).LerJsonAsync();
        Assert.DoesNotContain(lista.GetProperty("itens").EnumerateArray(), l => l.GetProperty("id").GetInt64() == lead.Id);
    }

    [Fact]
    public async Task Atendente_recebe_403_em_todas_as_rotas_da_importacao()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var arquivo = ImportacoesDeTeste.ArquivoDoExcel(ImportacoesDeTeste.LinhaExcel("Maria Souza"));
        var id = (await admin.ImportarAsync(arquivo)).GetProperty("id").GetInt64();

        var respostas = new[]
        {
            await atendente.PostAnaliseAsync(arquivo),
            await atendente.PostImportacaoAsync(ImportacoesDeTeste.CorpoImportacao(arquivo, await admin.AnalisarAsync(arquivo))),
            await atendente.GetAsync($"/api/importacoes/{id}", Ct),
            await atendente.GetAsync($"/api/importacoes/{id}/erros", Ct),
        };

        Assert.All(respostas, r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
    }

    [Fact]
    public async Task Arquivo_recusado_responde_400_com_o_motivo_no_campo_arquivo()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var muitasLinhas = ImportacoesDeTeste.ArquivoDoExcel(
            [.. Enumerable.Range(0, ArquivoCsv.MaximoDeLinhas + 1).Select(i => ImportacoesDeTeste.LinhaExcel($"Lead {i}", email: $"lead{i}@example.com"))]);

        var casos = new (byte[] Arquivo, string Mensagem)[]
        {
            ([0x50, 0x4B, 0x03, 0x04, 0x14, 0x00], ArquivoCsv.MensagemPlanilhaDoExcel),
            (new byte[ArquivoCsv.TamanhoMaximo + 1], ArquivoCsv.MensagemTamanho),
            ([], ArquivoCsv.MensagemVazio),
            (muitasLinhas, ArquivoCsv.MensagemLinhas),
        };

        foreach (var (arquivo, mensagem) in casos)
        {
            var resposta = await admin.PostAnaliseAsync(arquivo);
            Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
            Assert.Equal([mensagem], await resposta.LerErrosDoCampoAsync("arquivo"));
        }

        var base64Invalido = await admin.PostAsJsonAsync("/api/importacoes/analise", new { nomeArquivo = "x.csv", conteudoBase64 = "@@@" }, Ct);
        Assert.Equal([ImportacoesController.MensagemArquivoIncompleto], await base64Invalido.LerErrosDoCampoAsync("arquivo"));
    }

    [Fact]
    public async Task Mapeamento_e_opcoes_invalidas_respondem_400_com_o_motivo()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var arquivo = ImportacoesDeTeste.ArquivoDoExcel(ImportacoesDeTeste.LinhaExcel("Maria Souza"));
        var analise = await admin.AnalisarAsync(arquivo);

        async Task<HttpResponseMessage> ImportarAsync(Action<Dictionary<string, object?>> ajustar) =>
            await admin.PostImportacaoAsync(Corpo(arquivo, analise, ajustar));

        var semNome = await ImportarAsync(c => c["campos"] = new[] { "nao_importar", "email", "telefone", "empresa", "resposta_extra" });
        var semContato = await ImportarAsync(c => c["campos"] = new[] { "nome", "nao_importar", "nao_importar", "empresa", "resposta_extra" });
        var repetido = await ImportarAsync(c => c["campos"] = new[] { "nome", "email", "email", "empresa", "resposta_extra" });
        var colunasAMenos = await ImportarAsync(c => c["campos"] = new[] { "nome", "email" });
        var colunaDeIdForaDoArquivo = await ImportarAsync(c => c["colunaIdExterno"] = 5);
        var origemManual = await ImportarAsync(c => c["origem"] = "manual");
        var duasCampanhas = await ImportarAsync(c =>
        {
            c["campanhaPadraoId"] = 1;
            c["novaCampanha"] = "Feira";
        });
        var responsavelInexistente = await ImportarAsync(c => c["responsavelId"] = Guid.NewGuid());

        Assert.Equal([CriarImportacaoRequestValidator.MensagemNome], await semNome.LerErrosDoCampoAsync("campos"));
        Assert.Equal([CriarImportacaoRequestValidator.MensagemContato], await semContato.LerErrosDoCampoAsync("campos"));
        Assert.Equal([CriarImportacaoRequestValidator.MensagemCampoRepetido], await repetido.LerErrosDoCampoAsync("campos"));
        Assert.Equal([ImportacoesController.MensagemColunas], await colunasAMenos.LerErrosDoCampoAsync("campos"));
        Assert.Equal([ImportacoesController.MensagemColunaIdExterno], await colunaDeIdForaDoArquivo.LerErrosDoCampoAsync("colunaIdExterno"));
        Assert.Equal([CriarImportacaoRequestValidator.MensagemOrigem], await origemManual.LerErrosDoCampoAsync("origem"));
        Assert.Equal([CriarImportacaoRequestValidator.MensagemCampanha], await duasCampanhas.LerErrosDoCampoAsync("novaCampanha"));
        Assert.Equal([LeadsController.MensagemResponsavelInvalido], await responsavelInexistente.LerErrosDoCampoAsync("responsavelId"));
    }

    [Fact]
    public async Task A_analise_le_o_arquivo_sem_gravar_nada()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var email = LeadsDeTeste.EmailUnico();
        var nomeArquivo = $"analise-{Guid.NewGuid():N}.csv";

        var analise = await admin.AnalisarAsync(
            ImportacoesDeTeste.ArquivoDoExcel(ImportacoesDeTeste.LinhaExcel("Maria Souza", email: email)), nomeArquivo);

        Assert.Equal(nomeArquivo, analise.GetProperty("nomeArquivo").GetString());
        Assert.Equal(1, analise.GetProperty("totalLinhas").GetInt32());
        Assert.Empty(await fabrica.LeadsComEmailAsync(email));
        await using var escopo = fabrica.Services.CreateAsyncScope();
        Assert.False(await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().Importacoes.AnyAsync(i => i.NomeArquivo == nomeArquivo, Ct));
    }

    [Fact]
    public async Task Importacao_inexistente_responde_404()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/importacoes/999999999", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/importacoes/999999999/erros", Ct)).StatusCode);
    }

    [Fact]
    public async Task Importacao_fica_na_auditoria_sem_o_nome_do_arquivo()
    {
        var eu = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        using var admin = await fabrica.LogarAsync(eu);
        var arquivo = ImportacoesDeTeste.ArquivoDoExcel(ImportacoesDeTeste.LinhaExcel("Maria Souza"));

        var resultado = await admin.ImportarAsync(arquivo, corpo => corpo["nomeArquivo"] = "Leads da Maria Souza.csv");

        var registro = Assert.Single(await fabrica.AuditoriaAsync(EntidadesAuditoria.Importacao, resultado.GetProperty("id").GetInt64().ToString()));
        Assert.Equal(AcoesAuditoria.ImportacaoCriada, registro.Acao);
        Assert.Equal(eu.Id, registro.UsuarioId);
        var detalhes = registro.Detalhes!.RootElement;
        Assert.Equal(["linhas", "origem"], detalhes.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal("planilha", detalhes.GetProperty("origem").GetString());
        Assert.Equal(1, detalhes.GetProperty("linhas").GetInt32());
        Assert.Equal("Leads da Maria Souza.csv", resultado.GetProperty("nomeArquivo").GetString());
        Assert.Equal(eu.Nome, resultado.GetProperty("importadoPor").GetString());
    }

    private static Dictionary<string, object?> Corpo(byte[] arquivo, JsonElement analise, Action<Dictionary<string, object?>> ajustar)
    {
        var corpo = ImportacoesDeTeste.CorpoImportacao(arquivo, analise);
        ajustar(corpo);
        return corpo;
    }
}
