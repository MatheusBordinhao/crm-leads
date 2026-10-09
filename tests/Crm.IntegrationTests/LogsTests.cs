using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Datas;
using Crm.Api.Infra.TempoReal;
using Crm.IntegrationTests.Infra;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Crm.IntegrationTests;

/// <summary>Nenhum dado pessoal em log (regra que não pode quebrar).</summary>
public class LogsTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task O_corpo_da_integracao_nunca_aparece_no_log_nem_no_nivel_trace()
    {
        // Todos os níveis, inclusive os que a configuração deixa de fora (Trace e Debug).
        var captura = new CapturaDeLogs();
        using var api = fabrica.WithWebHostBuilder(builder => builder.ConfigureLogging(logging =>
            logging.AddProvider(captura).AddFilter<CapturaDeLogs>(_ => true)));
        using var n8n = api.CriarClienteIntegracao();
        var marca = Guid.NewGuid().ToString("N")[..10];
        var corpo = LeadsDeTeste.CorpoIntegracao();
        corpo["nome"] = $"Nome Sigiloso {marca}";
        corpo["empresa"] = $"Empresa Sigilosa {marca}";
        corpo["cpfCnpj"] = LeadsDeTeste.CpfUnico();
        corpo["respostasExtras"] = new Dictionary<string, string> { ["Pergunta"] = $"Resposta sigilosa {marca}" };
        string[] sensiveis =
        [
            (string)corpo["nome"]!,
            (string)corpo["email"]!,
            (string)corpo["telefone"]!,
            (string)corpo["cpfCnpj"]!,
            (string)corpo["empresa"]!,
            $"Resposta sigilosa {marca}",
            CrmApiFactory.ChaveIntegracao,
        ];

        // Criado, já recebido, vinculado por outra origem, recusado pela validação, JSON malformado e sem a chave.
        Assert.Equal(HttpStatusCode.Created, (await n8n.EnviarLeadAsync(corpo)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await n8n.EnviarLeadAsync(corpo)).StatusCode);
        var outraOrigem = new Dictionary<string, object?>(corpo) { ["origem"] = "site", ["idExterno"] = LeadsDeTeste.IdExternoUnico() };
        Assert.Equal(HttpStatusCode.OK, (await n8n.EnviarLeadAsync(outraOrigem)).StatusCode);
        var origemInvalida = new Dictionary<string, object?>(corpo) { ["origem"] = "whatsapp" };
        Assert.Equal(HttpStatusCode.BadRequest, (await n8n.EnviarLeadAsync(origemInvalida)).StatusCode);
        var malformado = new StringContent(
            $"{{ \"nome\": {JsonSerializer.Serialize(corpo["nome"])}, \"email\": {JsonSerializer.Serialize(corpo["email"])}, x }}",
            Encoding.UTF8,
            "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await n8n.PostAsync("/api/integracoes/leads", malformado, Ct)).StatusCode);
        using var semChave = api.CriarClienteIntegracao("chave-errada-mas-com-o-mesmo-tamanho-0123");
        Assert.Equal(HttpStatusCode.Unauthorized, (await semChave.EnviarLeadAsync(corpo)).StatusCode);

        AssertSemVazamento(captura, sensiveis);
    }

    [Fact]
    public async Task Senhas_e_dados_da_tela_nunca_aparecem_no_log_nem_no_nivel_trace()
    {
        var captura = new CapturaDeLogs();
        using var api = fabrica.WithWebHostBuilder(builder => builder.ConfigureLogging(logging =>
            logging.AddProvider(captura).AddFilter<CapturaDeLogs>(_ => true)));
        var marca = Guid.NewGuid().ToString("N")[..10];
        var eu = await api.CriarUsuarioAsync(Perfil.Admin);
        using var admin = await api.LogarAsync(eu);
        using var errado = api.CriarClienteHttps();
        await errado.PostLoginAsync(eu.Email, $"senha-errada-{marca}");

        var corpo = LeadsDeTeste.CorpoLead($"Nome Sigiloso {marca}");
        corpo["telefone"] = LeadsDeTeste.TelefoneUnico();
        corpo["cpfCnpj"] = LeadsDeTeste.CpfUnico();
        var id = await admin.CriarLeadIdAsync(corpo);
        var versao = (await (await admin.GetAsync($"/api/leads/{id}", Ct)).LerJsonAsync()).GetProperty("versao").GetUInt32();
        var edicao = new Dictionary<string, object?>(corpo) { ["empresa"] = $"Empresa Sigilosa {marca}", ["versao"] = versao };
        edicao.Remove("origem");
        edicao.Remove("responsavelId");
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/leads/{id}", edicao, Ct)).StatusCode);
        var emailNovo = $"sigiloso-{marca}@example.com";
        var usuarioNovo = new { nome = $"Usuária Sigilosa {marca}", email = emailNovo, perfil = "Atendente", participaRodizio = false, senhaProvisoria = $"provisoria-{marca}" };
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/usuarios", usuarioNovo, Ct)).StatusCode);

        AssertSemVazamento(
            captura,
            [
                eu.Email!,
                UsuariosDeTeste.SenhaPadrao,
                $"senha-errada-{marca}",
                (string)corpo["nome"]!,
                (string)corpo["email"]!,
                (string)corpo["telefone"]!,
                (string)corpo["cpfCnpj"]!,
                $"Empresa Sigilosa {marca}",
                usuarioNovo.nome,
                emailNovo,
                usuarioNovo.senhaProvisoria,
            ]);
    }

    [Fact]
    public async Task Textos_livres_do_funil_nunca_aparecem_no_log_nem_no_nivel_trace()
    {
        var captura = new CapturaDeLogs();
        using var api = fabrica.WithWebHostBuilder(builder => builder.ConfigureLogging(logging =>
            logging.AddProvider(captura).AddFilter<CapturaDeLogs>(_ => true)));
        var marca = Guid.NewGuid().ToString("N")[..10];
        using var atendente = await api.LogarComoAsync(Perfil.Atendente);
        var id = await atendente.CriarLeadIdAsync();
        var detalhe = $"Detalhe sigiloso {marca}";
        var descricao = $"Conversa sigilosa {marca}";

        var interacao = await atendente.RegistrarInteracaoAsync(id, "ligacao", descricao);
        var interacaoRecusada = await atendente.RegistrarInteracaoAsync(id, "ligacao", descricao, DateTimeOffset.UtcNow.AddDays(1));
        var linhaDoTempo = await atendente.LinhaDoTempoAsync(id);
        var perda = await atendente.MudarEtapaAsync(id, (await api.EtapaAsync("Perdido")).Id, await api.MotivoPerdaIdAsync(), detalhe);
        var recusada = await atendente.MudarEtapaAsync(id, (await api.EtapaAsync("Em contato")).Id, null, detalhe);

        Assert.Equal(HttpStatusCode.Created, interacao.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, interacaoRecusada.StatusCode);
        Assert.NotEmpty(linhaDoTempo);
        Assert.Equal(HttpStatusCode.OK, perda.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, recusada.StatusCode);
        AssertSemVazamento(captura, [detalhe, descricao]);
    }

    [Fact]
    public async Task Troca_de_responsavel_e_notificacoes_nunca_poem_dados_pessoais_no_log_nem_no_nivel_trace()
    {
        var captura = new CapturaDeLogs();
        using var api = fabrica.WithWebHostBuilder(builder => builder.ConfigureLogging(logging =>
            logging.AddProvider(captura).AddFilter<CapturaDeLogs>(_ => true)));
        var marca = Guid.NewGuid().ToString("N")[..10];
        var atendenteUsuario = await api.CriarUsuarioAsync();
        using var admin = await api.LogarComoAsync(Perfil.Admin);
        using var atendente = await api.LogarAsync(atendenteUsuario);
        var corpo = LeadsDeTeste.CorpoLead($"Nome Sigiloso {marca}");
        corpo["empresa"] = $"Empresa Sigilosa {marca}";
        var id = await admin.CriarLeadIdAsync(corpo);

        var troca = await admin.PostAsJsonAsync($"/api/leads/{id}/responsavel", new { responsavelId = atendenteUsuario.Id }, Ct);
        var lista = await atendente.NotificacoesAsync();
        await atendente.PostAsync("/api/notificacoes/lidas", null, Ct);

        Assert.Equal(HttpStatusCode.OK, troca.StatusCode);
        Assert.Contains(lista.Itens(), n => n.GetProperty("lead").GetProperty("nome").GetString() == (string)corpo["nome"]!);
        AssertSemVazamento(captura, [(string)corpo["nome"]!, (string)corpo["email"]!, $"Empresa Sigilosa {marca}"]);
    }

    [Fact]
    public async Task A_importacao_nunca_poe_dados_pessoais_nem_o_nome_do_arquivo_no_log_nem_no_nivel_trace()
    {
        var captura = new CapturaDeLogs();
        using var api = fabrica.WithWebHostBuilder(builder => builder.ConfigureLogging(logging =>
            logging.AddProvider(captura).AddFilter<CapturaDeLogs>(_ => true)));
        var marca = Guid.NewGuid().ToString("N")[..10];
        using var admin = await api.LogarComoAsync(Perfil.Admin);
        var telefone = LeadsDeTeste.TelefoneUnico();
        var linha = ImportacoesDeTeste.LinhaExcel(
            $"Nome Sigiloso {marca}", celular: LeadsDeTeste.ComoDigitado(telefone), empresa: $"Empresa Sigilosa {marca}", observacao: $"Observação sigilosa {marca}");
        var semNome = ImportacoesDeTeste.LinhaExcel(string.Empty);
        var nomeArquivo = $"Leads de Fulano Sigiloso {marca}.csv";

        var resultado = await admin.ImportarAsync(ImportacoesDeTeste.ArquivoDoExcel(linha, semNome), nomeArquivo: nomeArquivo);

        Assert.Equal(1, resultado.GetProperty("criados").GetInt32());
        Assert.Equal(1, resultado.GetProperty("comErro").GetInt32());
        Assert.Contains(captura.Registros, r => r.Contains($"Importação {resultado.GetProperty("id").GetInt64()} concluída", StringComparison.Ordinal));
        AssertSemVazamento(captura, [.. linha.Where(v => v.Length > 0), semNome[1], telefone, nomeArquivo]);
    }

    [Fact]
    public async Task O_tempo_real_nunca_poe_dados_pessoais_no_log_nem_no_nivel_trace()
    {
        var captura = new CapturaDeLogs();
        using var api = fabrica.WithWebHostBuilder(builder => builder.ConfigureLogging(logging =>
            logging.AddProvider(captura).AddFilter<CapturaDeLogs>(_ => true)));
        var marca = Guid.NewGuid().ToString("N")[..10];
        var atendente = await api.CriarUsuarioAsync();
        var adminUsuario = await api.CriarUsuarioAsync(Perfil.Admin);
        await using var conexaoAtendente = await api.ConectarAsync(atendente);
        await using var conexaoAdmin = await api.ConectarAsync(adminUsuario);
        using var admin = await api.LogarAsync(adminUsuario);
        var corpo = LeadsDeTeste.CorpoLead($"Nome Sigiloso {marca}");
        corpo["telefone"] = LeadsDeTeste.TelefoneUnico();
        corpo["cpfCnpj"] = LeadsDeTeste.CpfUnico();
        corpo["empresa"] = $"Empresa Sigilosa {marca}";
        corpo["responsavelId"] = atendente.Id;

        var id = await admin.CriarLeadIdAsync(corpo);
        await conexaoAtendente.AguardarLeadAsync(id);
        await conexaoAtendente.AguardarAsync(a => a.Nome == nameof(IClienteTempoReal.NotificacaoNova));
        await conexaoAdmin.AguardarLeadAsync(id);

        AssertSemVazamento(
            captura,
            [(string)corpo["nome"]!, (string)corpo["email"]!, (string)corpo["telefone"]!, (string)corpo["cpfCnpj"]!, $"Empresa Sigilosa {marca}"]);
    }

    [Fact]
    public async Task O_relatorio_por_campanha_nunca_poe_dados_pessoais_no_log_nem_no_nivel_trace()
    {
        var captura = new CapturaDeLogs();
        using var api = fabrica.WithWebHostBuilder(builder => builder.ConfigureLogging(logging =>
            logging.AddProvider(captura).AddFilter<CapturaDeLogs>(_ => true)));
        var marca = Guid.NewGuid().ToString("N")[..10];
        using var admin = await api.LogarComoAsync(Perfil.Admin);
        using var n8n = api.CriarClienteIntegracao();
        var corpo = LeadsDeTeste.CorpoIntegracao();
        corpo["nome"] = $"Nome Sigiloso {marca}";
        corpo["empresa"] = $"Empresa Sigilosa {marca}";
        corpo["cpfCnpj"] = LeadsDeTeste.CpfUnico();
        corpo["campanha"] = new { id = LeadsDeTeste.IdExternoUnico(), nome = $"Campanha {marca}" };
        corpo["respostasExtras"] = new Dictionary<string, string> { ["Pergunta"] = $"Resposta sigilosa {marca}" };
        await n8n.EnviarLeadIdAsync(corpo);
        var hoje = HorarioBrasilia.Dia(DateTimeOffset.UtcNow);
        var periodo = $"de={hoje.AddDays(-1):yyyy-MM-dd}&ate={hoje.AddDays(1):yyyy-MM-dd}";

        // As quatro rotas com o lead no período, a integração sem datas e um período recusado pela validação.
        foreach (var (cliente, caminho) in new[]
        {
            (admin, $"/api/relatorios/campanhas?{periodo}"),
            (admin, $"/api/relatorios/campanhas/csv?{periodo}"),
            (n8n, $"/api/integracoes/relatorios/campanhas?{periodo}"),
            (n8n, $"/api/integracoes/relatorios/campanhas/csv?{periodo}"),
            (n8n, "/api/integracoes/relatorios/campanhas/csv"),
        })
        {
            Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync(caminho, Ct)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await n8n.GetAsync("/api/integracoes/relatorios/campanhas?de=2026-09-30&ate=2026-09-01", Ct)).StatusCode);

        AssertSemVazamento(
            captura,
            [
                (string)corpo["nome"]!,
                (string)corpo["email"]!,
                (string)corpo["telefone"]!,
                (string)corpo["cpfCnpj"]!,
                $"Empresa Sigilosa {marca}",
                $"Resposta sigilosa {marca}",
                CrmApiFactory.ChaveIntegracao,
            ]);
    }

    [Fact]
    public async Task A_LGPD_a_exportacao_e_a_auditoria_nunca_poem_dados_pessoais_no_log_nem_no_nivel_trace()
    {
        // Num banco próprio: com uma importação de outro teste processando, a anonimização responderia 409.
        var captura = new CapturaDeLogs();
        using var api = fabrica.ApiNoBanco(await fabrica.CriarBancoAsync("logs_lgpd"), builder => builder.ConfigureLogging(logging =>
            logging.AddProvider(captura).AddFilter<CapturaDeLogs>(_ => true)));
        var marca = Guid.NewGuid().ToString("N")[..10];
        using var admin = await api.LogarComoAsync(Perfil.Admin);
        using var n8n = api.CriarClienteIntegracao();
        var corpo = LeadsDeTeste.CorpoIntegracao();
        corpo["nome"] = $"Nome Sigiloso {marca}";
        corpo["empresa"] = $"Empresa Sigilosa {marca}";
        corpo["cpfCnpj"] = LeadsDeTeste.CpfUnico();
        corpo["gclid"] = $"gclid-sigiloso-{marca}";
        corpo["respostasExtras"] = new Dictionary<string, string> { ["Pergunta"] = $"Resposta sigilosa {marca}" };
        var id = await n8n.EnviarLeadIdAsync(corpo);
        (await admin.RegistrarInteracaoAsync(id, "ligacao", $"Conversa sigilosa {marca}")).EnsureSuccessStatusCode();
        var deTeste = LeadsDeTeste.CorpoIntegracao();
        deTeste["teste"] = true;
        deTeste["nome"] = $"Teste Sigiloso {marca}";
        var idDeTeste = await n8n.EnviarLeadIdAsync(deTeste);

        // Exportar a lista e os dados do titular, anonimizar, excluir o lead de teste e consultar a auditoria.
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/leads/exportacao", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/leads/{id}/dados-titular", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.AnonimizarAsync(id)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/leads/{idDeTeste}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/auditoria?entidade=lead&entidadeId={id}", Ct)).StatusCode);

        AssertSemVazamento(
            captura,
            [
                (string)corpo["nome"]!,
                (string)corpo["email"]!,
                (string)corpo["telefone"]!,
                (string)corpo["cpfCnpj"]!,
                $"Empresa Sigilosa {marca}",
                $"gclid-sigiloso-{marca}",
                $"Resposta sigilosa {marca}",
                $"Conversa sigilosa {marca}",
                (string)corpo["idExterno"]!,
                (string)deTeste["nome"]!,
                (string)deTeste["email"]!,
            ]);
    }

    private static void AssertSemVazamento(CapturaDeLogs captura, IEnumerable<string> sensiveis)
    {
        var registros = captura.Registros.ToList();
        Assert.Contains(registros, r => r.StartsWith("Trace ", StringComparison.Ordinal));
        foreach (var valor in sensiveis)
        {
            var vazados = registros.Where(r => r.Contains(valor, StringComparison.OrdinalIgnoreCase)).ToList();
            Assert.True(vazados.Count == 0, $"O log tem um dado pessoal ou secreto:{Environment.NewLine}{string.Join(Environment.NewLine, vazados)}");
        }
    }
}
