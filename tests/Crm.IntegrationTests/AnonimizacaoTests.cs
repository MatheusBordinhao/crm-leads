using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.Features.Leads;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.Datas;
using Crm.Api.Infra.Importacoes;
using Crm.IntegrationTests.Infra;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Crm.IntegrationTests;

/// <summary>
/// Anonimização do lead (seção 3.8 e critérios de aceite do Passo 10). Com uma importação processando, anonimizar responde
/// 409, e as importações dos outros testes atrapalhariam: estes testes usam um banco só deles.
/// </summary>
public class AnonimizacaoTests(CrmApiFactory fabrica)
{
    private static readonly SemaphoreSlim CriacaoDoBanco = new(1, 1);

    private static string? _banco;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Depois_de_anonimizar_nenhum_dado_pessoal_do_lead_fica_no_banco()
    {
        var banco = await BancoAsync();
        using var api = fabrica.ApiNoBanco(banco);
        var atendenteUsuario = await api.CriarUsuarioAsync();
        using var admin = await api.LogarComoAsync(Perfil.Admin);
        using var atendente = await api.LogarAsync(atendenteUsuario);
        using var n8n = api.CriarClienteIntegracao();
        var marca = LeadsDeTeste.Marca();
        var telefone = LeadsDeTeste.TelefoneUnico();
        var digitado = LeadsDeTeste.ComoDigitado(telefone);
        var cpf = LeadsDeTeste.CpfUnico();
        var corpo = LeadsDeTeste.CorpoIntegracao();
        corpo["idExterno"] = $"meta-{marca}";
        corpo["nome"] = $"Nome {marca}";
        corpo["telefone"] = telefone;
        corpo["cpfCnpj"] = cpf;
        corpo["empresa"] = $"Empresa {marca}";
        corpo["gclid"] = $"gclid-{marca}";
        corpo["respostasExtras"] = new Dictionary<string, string> { ["Qual serviço você procura?"] = $"Resposta {marca}" };
        var email = (string)corpo["email"]!;
        var id = await n8n.EnviarLeadIdAsync(corpo);

        // A atendente recebe o lead, com notificação e evento, e registra os contatos.
        await PassarAsync(admin, id, atendenteUsuario);
        (await atendente.RegistrarInteracaoAsync(id, "ligacao", $"Conversa {marca}")).EnsureSuccessStatusCode();
        (await atendente.RegistrarInteracaoAsync(id, "nota", $"Nota {marca}")).EnsureSuccessStatusCode();

        // A mesma pessoa volta pelo site e por uma planilha, que já terminou de processar.
        var site = new Dictionary<string, object?>(corpo) { ["origem"] = "site", ["idExterno"] = $"site-{marca}", ["plataforma"] = "site", ["campanha"] = null };
        Assert.Equal("vinculado", (await (await n8n.EnviarLeadAsync(site)).LerJsonAsync()).GetProperty("situacao").GetString());
        var planilha = await admin.ImportarAsync(ImportacoesDeTeste.ArquivoDoExcel(
            ImportacoesDeTeste.LinhaExcel($"Nome {marca}", email, digitado, $"Empresa {marca}", $"Observacao {marca}")));
        Assert.Equal(1, planilha.GetProperty("vinculados").GetInt32());

        // E é perdida, com um detalhe escrito pela atendente.
        (await atendente.MudarEtapaAsync(id, (await api.EtapaAsync("Perdido")).Id, await api.MotivoPerdaIdAsync(), $"Detalhe {marca}"))
            .EnsureSuccessStatusCode();

        string[] marcadores = [marca, email, telefone.TrimStart('+'), digitado, cpf];
        var antes = new Dictionary<string, HashSet<string>>();
        foreach (var marcador in marcadores)
        {
            antes[marcador] = [.. await VarreduraDoBanco.OndeApareceAsync(banco, marcador)];
        }

        var eventosAntes = await api.EventosDoLeadAsync(id);
        var notificacoesAntes = await api.NotificacoesDoUsuarioAsync(atendenteUsuario.Id, id);

        var resposta = await admin.AnonimizarAsync(id);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        // Antes, a varredura acha a pessoa em toda parte: é a prova de que ela procura nos lugares certos.
        Assert.Superset(
            new HashSet<string>
            {
                "entradas_recebidas.gclid",
                "entradas_recebidas.id_externo",
                "entradas_recebidas.payload_bruto",
                "entradas_recebidas.respostas_extras",
                "eventos_saida.payload",
                "historico_etapas.motivo_perda_detalhe",
                "interacoes.descricao",
                "leads.empresa",
                "leads.gclid",
                "leads.motivo_perda_detalhe",
                "leads.nome",
                "leads.respostas_extras",
            },
            antes[marca]);
        Assert.Superset(new HashSet<string> { "entradas_recebidas.payload_bruto", "leads.email" }, antes[email]);
        Assert.Superset(new HashSet<string> { "entradas_recebidas.payload_bruto", "leads.telefone" }, antes[telefone.TrimStart('+')]);
        Assert.Contains("entradas_recebidas.payload_bruto", antes[digitado]);
        Assert.Superset(new HashSet<string> { "entradas_recebidas.payload_bruto", "leads.cpf_cnpj" }, antes[cpf]);
        Assert.Equal(3, eventosAntes.Count);

        // Depois, nada, em nenhuma tabela: nem nas entradas e nas interações, nem no histórico, nos eventos e nas
        // notificações, nem no arquivo da importação, nem o id do lead na Meta.
        foreach (var marcador in marcadores)
        {
            var achadas = await VarreduraDoBanco.OndeApareceAsync(banco, marcador);
            Assert.True(achadas.Count == 0, $"\"{marcador}\" ainda aparece em: {string.Join(", ", achadas)}");
        }

        var entradas = await EntradasAsync(api, id);
        Assert.Equal(3, entradas.Count);
        Assert.All(entradas, e => Assert.StartsWith(IdExternoAnonimo.Prefixo, e.IdExterno, StringComparison.Ordinal));
        Assert.Contains(entradas, e => e.IdExterno == IdExternoAnonimo.De($"meta-{marca}"));
        Assert.Empty(await api.EventosDoLeadAsync(id));

        // As notificações ficam: o título nunca levou dados do lead.
        Assert.NotEmpty(notificacoesAntes);
        Assert.Equal(notificacoesAntes.Select(n => n.Titulo), (await api.NotificacoesDoUsuarioAsync(atendenteUsuario.Id, id)).Select(n => n.Titulo));
    }

    [Fact]
    public async Task Anonimizar_mantem_os_totais_do_painel_do_relatorio_e_do_funil()
    {
        // Num banco só deste teste, para comparar os totais do admin sem filtros, e com nomes únicos, para a ordem das linhas
        // não depender de um empate.
        using var api = fabrica.ApiNoBanco(await fabrica.CriarBancoAsync("anonimizacao_painel"));
        var marca = LeadsDeTeste.Marca();
        var adriana = await api.CriarUsuarioAsync(nome: $"Adriana {marca}");
        var pietra = await api.CriarUsuarioAsync(nome: $"Pietra {marca}");
        using var admin = await api.LogarComoAsync(Perfil.Admin);
        using var comAdriana = await api.LogarAsync(adriana);
        using var comPietra = await api.LogarAsync(pietra);
        using var n8n = api.CriarClienteIntegracao();
        var meta = new { id = $"meta-{marca}", nome = $"Meta {marca}" };
        var google = new { id = $"google-{marca}", nome = $"Google {marca}" };
        var ganho = (await api.EtapaAsync("Ganho")).Id;
        var perdido = (await api.EtapaAsync("Perdido")).Id;
        Dictionary<string, object?> Corpo(string origem, object campanha)
        {
            var corpo = LeadsDeTeste.CorpoIntegracao(origem);
            corpo["campanha"] = campanha;
            corpo["cpfCnpj"] = LeadsDeTeste.CpfUnico();
            return corpo;
        }

        // Ganho, depois de uma ligação.
        var ganhoComLigacao = await n8n.EnviarLeadIdAsync(Corpo("meta_lead_ads", meta));
        await PassarAsync(admin, ganhoComLigacao, adriana);
        (await comAdriana.RegistrarInteracaoAsync(ganhoComLigacao, "ligacao", $"Conversa {marca}")).EnsureSuccessStatusCode();
        (await comAdriana.MudarEtapaAsync(ganhoComLigacao, ganho)).EnsureSuccessStatusCode();

        // Perdido por preço, com detalhe.
        var perdidoPorPreco = await n8n.EnviarLeadIdAsync(Corpo("meta_lead_ads", meta));
        await PassarAsync(admin, perdidoPorPreco, pietra);
        (await comPietra.MudarEtapaAsync(perdidoPorPreco, perdido, await api.MotivoPerdaIdAsync("Preço"), $"Detalhe {marca}")).EnsureSuccessStatusCode();

        // Cadastrado pela Adriana, em contato depois de um WhatsApp.
        var corpoManual = LeadsDeTeste.CorpoLead($"Manual {marca}");
        corpoManual["origem"] = "telefone";
        var emContato = await comAdriana.CriarLeadIdAsync(corpoManual);
        (await comAdriana.RegistrarInteracaoAsync(emContato, "whatsapp")).EnsureSuccessStatusCode();
        (await comAdriana.MudarEtapaAsync(emContato, (await api.EtapaAsync("Em contato")).Id)).EnsureSuccessStatusCode();

        // Sem responsável, com uma segunda entrada pelo site.
        var corpoSemResponsavel = Corpo("google_ads", google);
        var semResponsavel = await n8n.EnviarLeadIdAsync(corpoSemResponsavel);
        var site = new Dictionary<string, object?>(corpoSemResponsavel)
        {
            ["origem"] = "site",
            ["idExterno"] = LeadsDeTeste.IdExternoUnico(),
            ["campanha"] = null,
            ["plataforma"] = "site",
        };
        Assert.Equal("vinculado", (await (await n8n.EnviarLeadAsync(site)).LerJsonAsync()).GetProperty("situacao").GetString());

        // Perdido e reaberto por uma entrada do Google.
        var corpoReaberto = Corpo("meta_lead_ads", meta);
        var reaberto = await n8n.EnviarLeadIdAsync(corpoReaberto);
        await PassarAsync(admin, reaberto, adriana);
        (await comAdriana.MudarEtapaAsync(reaberto, perdido, await api.MotivoPerdaIdAsync("Sem resposta"))).EnsureSuccessStatusCode();
        var doGoogle = new Dictionary<string, object?>(corpoReaberto)
        {
            ["origem"] = "google_ads",
            ["idExterno"] = LeadsDeTeste.IdExternoUnico(),
            ["campanha"] = google,
            ["plataforma"] = "google",
        };
        Assert.Equal("vinculado", (await (await n8n.EnviarLeadAsync(doGoogle)).LerJsonAsync()).GetProperty("situacao").GetString());
        Assert.Equal("Novo", (await api.BuscarLeadAsync(reaberto)).Etapa.Nome);

        // Um dia de folga para cada lado, caso o teste atravesse a meia-noite de Brasília.
        var hoje = HorarioBrasilia.Dia(DateTimeOffset.UtcNow);
        var periodo = $"de={hoje.AddDays(-1):yyyy-MM-dd}&ate={hoje.AddDays(1):yyyy-MM-dd}";
        var campanhaMeta = (await api.BuscarLeadAsync(ganhoComLigacao)).CampanhaId;
        async Task<List<string>> TotaisAsync() =>
        [
            await TextoAsync(admin, $"/api/painel?{periodo}"),
            await TextoAsync(admin, $"/api/painel?{periodo}&responsavelId={adriana.Id}"),
            await TextoAsync(admin, $"/api/painel?{periodo}&campanhaId={campanhaMeta}"),
            await TextoAsync(admin, $"/api/painel?{periodo}&semResponsavel=true"),
            await TextoAsync(comAdriana, $"/api/painel?{periodo}"),
            await TextoAsync(admin, $"/api/relatorios/campanhas?{periodo}"),
            await TextoAsync(admin, $"/api/relatorios/campanhas/csv?{periodo}"),
            TotaisDoFunil(JsonSerializer.Deserialize<JsonElement>(await TextoAsync(admin, "/api/funil"))),
        ];
        var antes = await TotaisAsync();

        foreach (var id in new[] { ganhoComLigacao, perdidoPorPreco, emContato, semResponsavel, reaberto })
        {
            Assert.Equal(HttpStatusCode.OK, (await admin.AnonimizarAsync(id)).StatusCode);
        }

        var depois = await TotaisAsync();

        Assert.Equal(antes, depois);

        // Os números são os da massa: 5 leads novos, 7 entradas, 1 ganho, 2 perdas e 4 leads com contato (a ligação, o
        // WhatsApp e as duas perdas, que tiram o lead da etapa inicial).
        var painel = JsonSerializer.Deserialize<JsonElement>(antes[0]);
        Assert.Equal(
            (5, 7, 1, 2, 4),
            (painel.GetProperty("leadsNovos").GetInt32(),
                painel.GetProperty("entradas").GetInt32(),
                painel.GetProperty("ganhos").GetInt32(),
                painel.GetProperty("perdas").GetProperty("leads").GetInt32(),
                painel.GetProperty("primeiroContato").GetProperty("comContato").GetInt32()));
        Assert.All(
            await Task.WhenAll(new[] { ganhoComLigacao, perdidoPorPreco, emContato, semResponsavel, reaberto }.Select(api.BuscarLeadAsync)),
            lead => Assert.NotNull(lead.AnonimizadoEm));
    }

    [Fact]
    public async Task Anonimizar_mantem_etapa_datas_origem_campanha_e_responsavel_e_entra_na_auditoria_uma_vez()
    {
        using var api = await CriarApiAsync();
        var adminUsuario = await api.CriarUsuarioAsync(Perfil.Admin);
        var atendenteUsuario = await api.CriarUsuarioAsync();
        using var admin = await api.LogarAsync(adminUsuario);
        using var atendente = await api.LogarAsync(atendenteUsuario);
        using var n8n = api.CriarClienteIntegracao();
        var corpo = LeadsDeTeste.CorpoIntegracao();
        corpo["cpfCnpj"] = LeadsDeTeste.CpfUnico();
        corpo["gclid"] = "gclid-do-teste";
        var id = await n8n.EnviarLeadIdAsync(corpo);
        await PassarAsync(admin, id, atendenteUsuario);
        (await atendente.RegistrarInteracaoAsync(id, "ligacao", "Conversa sobre o orçamento")).EnsureSuccessStatusCode();
        (await atendente.MudarEtapaAsync(id, (await api.EtapaAsync("Perdido")).Id, await api.MotivoPerdaIdAsync(), "Achou caro"))
            .EnsureSuccessStatusCode();
        var antes = await FichaAsync(atendente, id);
        var linhaAntes = await atendente.LinhaDoTempoAsync(id);
        var eventos = (await api.EventosDoLeadAsync(id)).Count;

        var resposta = await admin.AnonimizarAsync(id);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var ficha = await resposta.LerJsonAsync();
        Assert.Equal(Anonimizacao.NomeDoTitularRemovido, ficha.GetProperty("nome").GetString());
        foreach (var campo in new[] { "email", "telefone", "cpfCnpj", "empresa", "gclid", "motivoPerdaDetalhe" })
        {
            Assert.Equal(JsonValueKind.Null, ficha.GetProperty(campo).ValueKind);
        }

        Assert.Empty(ficha.GetProperty("respostasExtras").EnumerateArray());
        Assert.Equal(JsonValueKind.String, ficha.GetProperty("anonimizadoEm").ValueKind);
        foreach (var campo in new[]
        {
            "etapa", "responsavel", "origem", "plataforma", "campanha", "uf", "utmSource", "utmMedium", "utmCampaign", "teste",
            "criadoEm", "primeiroContatoEm", "perdidoEm", "motivoPerda",
        })
        {
            Assert.Equal(antes.GetProperty(campo).GetRawText(), ficha.GetProperty(campo).GetRawText());
        }

        // A atendente continua vendo o lead, e a linha do tempo tem os mesmos itens, sem a descrição e sem o detalhe.
        Assert.Equal(Anonimizacao.NomeDoTitularRemovido, (await FichaAsync(atendente, id)).GetProperty("nome").GetString());
        var linha = await atendente.LinhaDoTempoAsync(id);
        Assert.Equal(linhaAntes.Select(i => i.GetProperty("tipo").GetString()), linha.Select(i => i.GetProperty("tipo").GetString()));
        Assert.All(
            linha.Where(i => i.GetProperty("tipo").GetString() == "interacao"),
            i => Assert.Equal(JsonValueKind.Null, i.GetProperty("interacao").GetProperty("descricao").ValueKind));
        Assert.All(
            linha.Where(i => i.GetProperty("tipo").GetString() == "etapa"),
            i => Assert.Equal(JsonValueKind.Null, i.GetProperty("etapa").GetProperty("motivoPerdaDetalhe").ValueKind));

        // Na auditoria, só as contagens: a entrada, a ligação e os eventos do lead novo e da troca de responsável.
        var registro = Assert.Single(
            await api.AuditoriaAsync(EntidadesAuditoria.Lead, id.ToString(CultureInfo.InvariantCulture)), r => r.Acao == AcoesAuditoria.LeadAnonimizado);
        Assert.Equal(adminUsuario.Id, registro.UsuarioId);
        var detalhes = registro.Detalhes!.RootElement;
        Assert.Equal(2, eventos);
        Assert.Equal(
            (1, 1, eventos),
            (detalhes.GetProperty("entradas").GetInt32(), detalhes.GetProperty("interacoes").GetInt32(), detalhes.GetProperty("eventos").GetInt32()));

        // Anonimizar de novo não muda nada nem grava outra auditoria.
        var deNovo = await admin.AnonimizarAsync(id);
        Assert.Equal(HttpStatusCode.OK, deNovo.StatusCode);
        Assert.Equal(ficha.GetProperty("anonimizadoEm").GetString(), (await deNovo.LerJsonAsync()).GetProperty("anonimizadoEm").GetString());
        Assert.Single(
            await api.AuditoriaAsync(EntidadesAuditoria.Lead, id.ToString(CultureInfo.InvariantCulture)), r => r.Acao == AcoesAuditoria.LeadAnonimizado);
    }

    [Fact]
    public async Task Lead_anonimizado_nao_aceita_dados_nem_interacoes_mas_muda_de_etapa_e_de_responsavel()
    {
        using var api = await CriarApiAsync();
        var atendenteUsuario = await api.CriarUsuarioAsync();
        var outraUsuario = await api.CriarUsuarioAsync();
        using var admin = await api.LogarComoAsync(Perfil.Admin);
        using var atendente = await api.LogarAsync(atendenteUsuario);
        var id = await atendente.CriarLeadIdAsync();
        Assert.Equal(HttpStatusCode.OK, (await admin.AnonimizarAsync(id)).StatusCode);
        var ficha = await FichaAsync(atendente, id);

        var edicao = await atendente.PutAsJsonAsync(
            $"/api/leads/{id}",
            new
            {
                nome = "Maria Souza",
                email = LeadsDeTeste.EmailUnico(),
                telefone = (string?)null,
                cpfCnpj = (string?)null,
                uf = (string?)null,
                empresa = (string?)null,
                versao = ficha.GetProperty("versao").GetUInt32(),
            },
            Ct);
        var interacao = await atendente.RegistrarInteracaoAsync(id, "nota", "Ligou de novo");
        var etapa = await atendente.MudarEtapaAsync(id, (await api.EtapaAsync("Em contato")).Id);
        var troca = await admin.PostAsJsonAsync($"/api/leads/{id}/responsavel", new { responsavelId = outraUsuario.Id }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, edicao.StatusCode);
        Assert.Equal(Anonimizacao.MensagemDadosRemovidos, await edicao.LerDetalheAsync());
        Assert.Equal(HttpStatusCode.BadRequest, interacao.StatusCode);
        Assert.Equal(Anonimizacao.MensagemSemInteracoes, await interacao.LerDetalheAsync());
        Assert.Equal(HttpStatusCode.OK, etapa.StatusCode);
        Assert.Equal(HttpStatusCode.OK, troca.StatusCode);
        var lead = await api.BuscarLeadAsync(id);
        Assert.Equal(
            (Anonimizacao.NomeDoTitularRemovido, (string?)null, "Em contato", (Guid?)outraUsuario.Id),
            (lead.Nome, lead.Email, lead.Etapa.Nome, lead.ResponsavelId));
        Assert.Empty(await InteracoesAsync(api, id));
    }

    [Fact]
    public async Task Lead_anonimizado_vai_para_Perdido_so_com_o_motivo_e_o_detalhe_responde_400()
    {
        using var api = await CriarApiAsync();
        using var admin = await api.LogarComoAsync(Perfil.Admin);
        using var atendente = await api.LogarAsync(await api.CriarUsuarioAsync());
        var id = await atendente.CriarLeadIdAsync();
        Assert.Equal(HttpStatusCode.OK, (await admin.AnonimizarAsync(id)).StatusCode);
        var perdido = (await api.EtapaAsync("Perdido")).Id;
        var motivo = await api.MotivoPerdaIdAsync();
        var historicoAntes = (await api.HistoricoAsync(id)).Count;

        // O cartão do funil avisa a tela, que não oferece o detalhe no pedido do motivo.
        var quadro = await atendente.GetFromJsonAsync<JsonElement>("/api/funil", Ct);
        var cartao = quadro.GetProperty("colunas").EnumerateArray()
            .SelectMany(c => c.GetProperty("leads").EnumerateArray())
            .Single(c => c.GetProperty("id").GetInt64() == id);
        Assert.True(cartao.GetProperty("anonimizado").GetBoolean());

        // O detalhe é texto livre e poderia trazer de volta um dado pessoal.
        var comDetalhe = await atendente.MudarEtapaAsync(id, perdido, motivo, "Falei com a Maria pelo celular dela");

        Assert.Equal(HttpStatusCode.BadRequest, comDetalhe.StatusCode);
        Assert.Equal([Anonimizacao.MensagemSemDetalhePerda], await comDetalhe.LerErrosDoCampoAsync("motivoPerdaDetalhe"));
        Assert.Equal("Novo", (await api.BuscarLeadAsync(id)).Etapa.Nome);
        Assert.Equal(historicoAntes, (await api.HistoricoAsync(id)).Count);

        var semDetalhe = await atendente.MudarEtapaAsync(id, perdido, motivo);

        Assert.Equal(HttpStatusCode.OK, semDetalhe.StatusCode);
        var lead = await api.BuscarLeadAsync(id);
        Assert.Equal((perdido, (int?)motivo, (string?)null), (lead.EtapaId, lead.MotivoPerdaId, lead.MotivoPerdaDetalhe));
        var historico = (await api.HistoricoAsync(id)).Last();
        Assert.Equal((perdido, (int?)motivo, (string?)null), (historico.EtapaNovaId, historico.MotivoPerdaId, historico.MotivoPerdaDetalhe));
    }

    [Fact]
    public async Task Reenvio_e_reimportacao_do_lead_anonimizado_respondem_ja_recebido_sem_trazer_a_pessoa_de_volta()
    {
        using var api = await CriarApiAsync();
        using var admin = await api.LogarComoAsync(Perfil.Admin);
        using var n8n = api.CriarClienteIntegracao();
        var corpo = LeadsDeTeste.CorpoIntegracao();
        var id = await n8n.EnviarLeadIdAsync(corpo);

        // Uma planilha sem coluna de id: o id externo é o hash da linha.
        var linha = ImportacoesDeTeste.LinhaExcel("Lead da planilha");
        var arquivo = ImportacoesDeTeste.ArquivoDoExcel(linha);
        var importacao = await admin.ImportarAsync(arquivo);
        var importado = Assert.Single(await api.LeadsDaImportacaoAsync(importacao.GetProperty("id").GetInt64())).Id;
        Assert.Equal(HttpStatusCode.OK, (await admin.AnonimizarAsync(id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.AnonimizarAsync(importado)).StatusCode);

        var reenvio = await n8n.EnviarLeadAsync(corpo);
        var reimportacao = await admin.ImportarAsync(arquivo);

        Assert.Equal(HttpStatusCode.OK, reenvio.StatusCode);
        var resultado = await reenvio.LerJsonAsync();
        Assert.Equal(("ja_recebido", id), (resultado.GetProperty("situacao").GetString(), resultado.GetProperty("leadId").GetInt64()));
        Assert.Equal(
            (0, 0, 1),
            (reimportacao.GetProperty("criados").GetInt32(), reimportacao.GetProperty("vinculados").GetInt32(), reimportacao.GetProperty("jaImportados").GetInt32()));
        Assert.Empty(await api.LeadsComEmailAsync((string)corpo["email"]!));
        Assert.Empty(await api.LeadsComEmailAsync(linha[1]));
        Assert.Single(await EntradasAsync(api, id));
        Assert.Single(await EntradasAsync(api, importado));
    }

    [Fact]
    public async Task A_mesma_pessoa_com_outro_id_externo_vira_um_lead_novo()
    {
        using var api = await CriarApiAsync();
        using var admin = await api.LogarComoAsync(Perfil.Admin);
        using var n8n = api.CriarClienteIntegracao();
        var corpo = LeadsDeTeste.CorpoIntegracao();
        var id = await n8n.EnviarLeadIdAsync(corpo);
        Assert.Equal(HttpStatusCode.OK, (await admin.AnonimizarAsync(id)).StatusCode);

        var resposta = await n8n.EnviarLeadAsync(new Dictionary<string, object?>(corpo) { ["idExterno"] = LeadsDeTeste.IdExternoUnico() });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var novo = (await resposta.LerJsonAsync()).GetProperty("leadId").GetInt64();
        Assert.NotEqual(id, novo);
        Assert.Equal((string)corpo["email"]!, (await api.BuscarLeadAsync(novo)).Email);
        Assert.Null((await api.BuscarLeadAsync(id)).Email);
    }

    [Fact]
    public async Task Entrada_que_esperava_a_trava_do_lead_anonimizado_vira_um_lead_novo()
    {
        var banco = await BancoAsync();
        using var api = fabrica.ApiNoBanco(banco);
        using var n8n = api.CriarClienteIntegracao();
        var corpo = LeadsDeTeste.CorpoIntegracao();
        var id = await n8n.EnviarLeadIdAsync(corpo);

        // A anonimização, numa conexão à parte, segura a linha do lead enquanto a entrada chega.
        await using var conexao = new NpgsqlConnection(banco);
        await conexao.OpenAsync(Ct);
        await using var transacao = await conexao.BeginTransactionAsync(Ct);
        await ExecutarAsync(conexao, $"SELECT 1 FROM leads WHERE id = {id} FOR UPDATE");
        await using var pid = new NpgsqlCommand("SELECT pg_backend_pid()", conexao);
        var processo = (int)(await pid.ExecuteScalarAsync(Ct))!;

        // A mesma pessoa, com outro id: a entrada acha o lead e espera a trava.
        var envio = n8n.EnviarLeadAsync(new Dictionary<string, object?>(corpo) { ["idExterno"] = LeadsDeTeste.IdExternoUnico() });
        await AguardarBloqueadaPorAsync(banco, processo);
        await ExecutarAsync(
            conexao, $"UPDATE leads SET nome = 'Titular removido', email = NULL, telefone = NULL, empresa = NULL, anonimizado_em = now() WHERE id = {id}");
        await transacao.CommitAsync(Ct);
        var resposta = await envio;

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.NotEqual(id, (await resposta.LerJsonAsync()).GetProperty("leadId").GetInt64());
        var anonimizado = await api.BuscarLeadAsync(id);
        Assert.Equal(((string?)null, (string?)null, (string?)null), (anonimizado.Email, anonimizado.Telefone, anonimizado.Empresa));
        Assert.Single(await EntradasAsync(api, id));
    }

    [Fact]
    public async Task Com_uma_importacao_processando_anonimizar_responde_409_ate_ela_terminar()
    {
        // O serviço da importação retoma qualquer importação parada: sem ele, a importação fica processando.
        var banco = await fabrica.CriarBancoAsync("anonimizacao_importacao");
        using var api = fabrica.ApiNoBanco(banco, builder => builder.ConfigureTestServices(services =>
            services.Remove(services.Single(s => s.ImplementationType == typeof(ServicoImportacoes)))));
        using var admin = await api.LogarComoAsync(Perfil.Admin);
        using var n8n = api.CriarClienteIntegracao();
        var corpo = LeadsDeTeste.CorpoIntegracao();
        var id = await n8n.EnviarLeadIdAsync(corpo);
        var arquivo = ImportacoesDeTeste.ArquivoDoExcel(ImportacoesDeTeste.LinhaExcel("Lead da planilha", (string)corpo["email"]!));
        var criacao = await admin.PostImportacaoAsync(ImportacoesDeTeste.CorpoImportacao(arquivo, await admin.AnalisarAsync(arquivo)));
        Assert.Equal(HttpStatusCode.Accepted, criacao.StatusCode);
        var importacaoId = (await criacao.LerJsonAsync()).GetProperty("id").GetInt64();

        var recusada = await admin.AnonimizarAsync(id);

        Assert.Equal(HttpStatusCode.Conflict, recusada.StatusCode);
        Assert.Equal(Anonimizacao.MensagemImportacaoEmAndamento, await recusada.LerDetalheAsync());
        var intacto = await api.BuscarLeadAsync(id);
        Assert.Equal(((string)corpo["email"]!, (DateTimeOffset?)null), (intacto.Email, intacto.AnonimizadoEm));
        Assert.DoesNotContain(
            await api.AuditoriaAsync(EntidadesAuditoria.Lead, id.ToString(CultureInfo.InvariantCulture)), r => r.Acao == AcoesAuditoria.LeadAnonimizado);

        // Quando a importação termina, o arquivo dela é apagado, e a anonimização segue.
        await using (var escopo = api.Services.CreateAsyncScope())
        {
            await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().Importacoes
                .Where(i => i.Id == importacaoId)
                .ExecuteUpdateAsync(i => i.SetProperty(x => x.ReservadaAte, DateTimeOffset.UtcNow.AddMinutes(-1)), Ct);
            await escopo.ServiceProvider.GetRequiredService<ProcessadorImportacao>().RetomarInterrompidasAsync(Ct);
        }

        await admin.AguardarConclusaoAsync(importacaoId);
        Assert.Equal(HttpStatusCode.OK, (await admin.AnonimizarAsync(id)).StatusCode);
        Assert.Null((await api.BuscarLeadAsync(id)).Email);
    }

    [Fact]
    public async Task Atendente_recebe_403_e_lead_que_nao_existe_responde_404()
    {
        using var api = await CriarApiAsync();
        using var atendente = await api.LogarComoAsync(Perfil.Atendente);
        using var admin = await api.LogarComoAsync(Perfil.Admin);
        var id = await atendente.CriarLeadIdAsync();

        var daAtendente = await atendente.AnonimizarAsync(id);
        var inexistente = await admin.AnonimizarAsync(long.MaxValue);

        Assert.Equal(HttpStatusCode.Forbidden, daAtendente.StatusCode);
        Assert.Null((await api.BuscarLeadAsync(id)).AnonimizadoEm);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
    }

    /// <summary>O banco destes testes, criado uma vez para a classe. Os testes da classe rodam um depois do outro.</summary>
    private async Task<string> BancoAsync()
    {
        await CriacaoDoBanco.WaitAsync(Ct);
        try
        {
            return _banco ??= await fabrica.CriarBancoAsync("anonimizacao");
        }
        finally
        {
            CriacaoDoBanco.Release();
        }
    }

    private async Task<WebApplicationFactory<Program>> CriarApiAsync() => fabrica.ApiNoBanco(await BancoAsync());

    private static async Task PassarAsync(HttpClient admin, long leadId, Usuario para) =>
        (await admin.PostAsJsonAsync($"/api/leads/{leadId}/responsavel", new { responsavelId = para.Id }, Ct)).EnsureSuccessStatusCode();

    private static async Task<JsonElement> FichaAsync(HttpClient cliente, long id)
    {
        var resposta = await cliente.GetAsync($"/api/leads/{id}", Ct);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return await resposta.LerJsonAsync();
    }

    private static async Task<string> TextoAsync(HttpClient cliente, string caminho)
    {
        var resposta = await cliente.GetAsync(caminho, Ct);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return await resposta.Content.ReadAsStringAsync(Ct);
    }

    /// <summary>Os totais do quadro: quantos leads cada coluna tem, e quantos foram para Ganho e para Perdido.</summary>
    private static string TotaisDoFunil(JsonElement quadro) => string.Join(
        ';',
        quadro.GetProperty("colunas").EnumerateArray()
            .Select(c => $"{c.GetProperty("etapa").GetProperty("id").GetInt32()}:{c.GetProperty("total").GetInt32()}")
            .Append($"ganho:{quadro.GetProperty("ganho").GetProperty("periodo").GetInt32()}")
            .Append($"perdido:{quadro.GetProperty("perdido").GetProperty("periodo").GetInt32()}"));

    private static async Task<List<EntradaRecebida>> EntradasAsync(WebApplicationFactory<Program> api, long leadId)
    {
        await using var escopo = api.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().EntradasRecebidas
            .AsNoTracking()
            .Where(e => e.LeadId == leadId)
            .OrderBy(e => e.Id)
            .ToListAsync(Ct);
    }

    private static async Task<List<Interacao>> InteracoesAsync(WebApplicationFactory<Program> api, long leadId)
    {
        await using var escopo = api.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().Interacoes
            .AsNoTracking()
            .Where(i => i.LeadId == leadId)
            .ToListAsync(Ct);
    }

    private static async Task ExecutarAsync(NpgsqlConnection conexao, string sql)
    {
        await using var comando = new NpgsqlCommand(sql, conexao);
        await comando.ExecuteNonQueryAsync(Ct);
    }

    /// <summary>Espera alguma conexão ficar parada numa trava que o processo <paramref name="processo"/> segura.</summary>
    private static async Task AguardarBloqueadaPorAsync(string banco, int processo)
    {
        await using var conexao = new NpgsqlConnection(banco);
        await conexao.OpenAsync(Ct);
        await using var comando = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE @processo = ANY(pg_blocking_pids(pid)))", conexao);
        comando.Parameters.AddWithValue("processo", processo);
        var limite = DateTime.UtcNow.AddSeconds(15);
        while (!(bool)(await comando.ExecuteScalarAsync(Ct))!)
        {
            Assert.True(DateTime.UtcNow < limite, "A entrada não chegou a esperar a trava do lead.");
            await Task.Delay(50, Ct);
        }
    }
}
