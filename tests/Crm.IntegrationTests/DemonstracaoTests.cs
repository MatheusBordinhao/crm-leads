using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.Datas;
using Crm.Api.Infra.Demonstracao;
using Crm.IntegrationTests.Infra;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Crm.IntegrationTests;

/// <summary>
/// O comando que prepara a demonstração (README › Demonstração). Ele apaga todos os leads, então cada teste usa um banco
/// só dele, no mesmo contêiner.
/// </summary>
public class DemonstracaoTests(CrmApiFactory fabrica)
{
    private const string EnderecoLocal = "http://localhost:5173";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A API num banco à parte, com a permissão e o endereço pedidos e, se houver, um relógio parado.</summary>
    private WebApplicationFactory<Program> CriarApi(
        string banco, bool permitir = true, string urlPublica = EnderecoLocal, TimeProvider? relogio = null) =>
        fabrica.WithWebHostBuilder(builder =>
        {
            builder.UseSetting($"ConnectionStrings:{DadosExtensions.NomeConexao}", banco);
            builder.UseSetting("Crm:UrlPublica", urlPublica);
            if (permitir)
            {
                builder.UseSetting(PreparacaoDemonstracao.ChavePermissao, "true");
            }

            if (relogio is not null)
            {
                builder.ConfigureTestServices(services => services.AddSingleton(relogio));
            }
        });

    private static async Task<ResultadoDemonstracao> PrepararAsync(WebApplicationFactory<Program> api)
    {
        await using var escopo = api.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<PreparacaoDemonstracao>().ExecutarAsync(Ct);
    }

    private static CrmDbContext Banco(string banco) => new(DadosExtensions.CriarOpcoes(banco));

    /// <summary>A Adriana e a Pietra, as duas no rodízio, que recebe os leads da integração.</summary>
    private static async Task<(Usuario Adriana, Usuario Pietra)> CriarAtendentesAsync(WebApplicationFactory<Program> api, string banco)
    {
        var adriana = await api.CriarUsuarioAsync(nome: "Adriana");
        var pietra = await api.CriarUsuarioAsync(nome: "Pietra");
        await using var db = Banco(banco);
        await db.Users.Where(u => u.Id == adriana.Id || u.Id == pietra.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.ParticipaRodizio, true), Ct);
        return (adriana, pietra);
    }

    /// <summary>O que não pode mudar: usuários, etapas, motivos e configurações.</summary>
    private static async Task<string> RetratoDoQueFicaAsync(string banco)
    {
        await using var db = Banco(banco);
        return JsonSerializer.Serialize(new
        {
            usuarios = await db.Users.OrderBy(u => u.Id).Select(u => new { u.Id, u.Nome, u.Email, u.Ativo, u.ParticipaRodizio }).ToListAsync(Ct),
            etapas = await db.Etapas.OrderBy(e => e.Id).Select(e => new { e.Id, e.Nome, e.Ordem, e.Tipo, e.Ativa }).ToListAsync(Ct),
            motivos = await db.MotivosPerda.OrderBy(m => m.Id).Select(m => new { m.Id, m.Nome, m.Ativo }).ToListAsync(Ct),
            configuracoes = await db.Configuracoes.OrderBy(c => c.Chave).Select(c => new { c.Chave, c.Valor }).ToListAsync(Ct),
        });
    }

    /// <summary>Os números do painel que não dependem dos ids, para comparar duas rodadas.</summary>
    private static async Task<string> NumerosDoPainelAsync(HttpClient admin)
    {
        var painel = await (await admin.GetAsync("/api/painel", Ct)).LerJsonAsync();
        return JsonSerializer.Serialize(new
        {
            leadsNovos = painel.GetProperty("leadsNovos").GetInt32(),
            entradas = painel.GetProperty("entradas").GetInt32(),
            ganhos = painel.GetProperty("ganhos").GetInt32(),
            mediana = painel.GetProperty("primeiroContato").GetProperty("medianaMinutos").GetDouble(),
            situacao = painel.GetProperty("situacaoAtual").EnumerateArray().Select(s => s.GetProperty("leadsNovos").GetInt32()).ToList(),
            campanhas = painel.GetProperty("porCampanha").EnumerateArray()
                .Select(c => $"{(c.GetProperty("campanha").ValueKind == JsonValueKind.Null ? "-" : c.GetProperty("campanha").GetProperty("nome").GetString())}: {c.GetProperty("leadsNovos").GetInt32()}/{c.GetProperty("ganhos").GetInt32()}")
                .ToList(),
            perdas = painel.GetProperty("perdas").GetProperty("leads").GetInt32(),
            atendentes = painel.GetProperty("porAtendente").EnumerateArray().Select(a => a.GetProperty("leadsNovos").GetInt32()).ToList(),
        });
    }

    [Fact]
    public async Task Sem_a_permissao_fora_de_localhost_ou_sem_atendente_ativa_recusa_e_nada_muda()
    {
        var banco = await fabrica.CriarBancoAsync("demonstracao");
        using (var api = CriarApi(banco))
        {
            await CriarAtendentesAsync(api, banco);
            using var n8n = api.CriarClienteIntegracao();
            await n8n.EnviarLeadIdAsync(LeadsDeTeste.CorpoIntegracao());
        }

        using var semPermissao = CriarApi(banco, permitir: false);
        using var foraDeLocalhost = CriarApi(banco, urlPublica: "https://crm.empresa.com.br");
        var recusas = new List<ResultadoDemonstracao> { await PrepararAsync(semPermissao), await PrepararAsync(foraDeLocalhost) };
        await using (var db = Banco(banco))
        {
            await db.Atendentes().ExecuteUpdateAsync(u => u.SetProperty(x => x.Ativo, false), Ct);
        }

        using var semAtendente = CriarApi(banco);
        recusas.Add(await PrepararAsync(semAtendente));

        Assert.Equal(
            [PreparacaoDemonstracao.MensagemSemPermissao, PreparacaoDemonstracao.MensagemForaDeLocalhost, PreparacaoDemonstracao.MensagemSemAtendentes],
            recusas.Select(r => r.Mensagem));
        Assert.All(recusas, r => Assert.False(r.Preparada));
        await using var depois = Banco(banco);
        Assert.Equal(1, await depois.Leads.CountAsync(Ct));
        Assert.Equal(0, await depois.Campanhas.CountAsync(c => c.IdExterno.StartsWith("demo-"), Ct));
    }

    [Fact]
    public async Task Troca_os_leads_pela_massa_e_mantem_usuarios_etapas_motivos_e_configuracoes()
    {
        var banco = await fabrica.CriarBancoAsync("demonstracao");
        using var api = CriarApi(banco);
        var (adriana, _) = await CriarAtendentesAsync(api, banco);
        using var admin = await api.LogarComoAsync(Perfil.Admin);
        using var n8n = api.CriarClienteIntegracao();

        // O que já existia: um lead da integração, com notificação e evento para o n8n, uma interação, um lead de teste,
        // uma importação com o resumo e uma configuração mudada.
        var daIntegracao = await n8n.EnviarLeadIdAsync(LeadsDeTeste.CorpoIntegracao());
        (await admin.RegistrarInteracaoAsync(daIntegracao, "ligacao")).EnsureSuccessStatusCode();
        var corpoTeste = LeadsDeTeste.CorpoIntegracao();
        corpoTeste["teste"] = true;
        await n8n.EnviarLeadIdAsync(corpoTeste);
        await admin.ImportarAsync(ImportacoesDeTeste.ArquivoDoExcel(ImportacoesDeTeste.LinhaExcel("Lead da planilha")));
        (await admin.PutAsJsonAsync("/api/configuracoes", new { exigirDocumentoParaGanho = false, distribuicaoAutomatica = true }, Ct)).EnsureSuccessStatusCode();
        List<long> antigos;
        await using (var db = Banco(banco))
        {
            antigos = await db.Leads.Select(l => l.Id).ToListAsync(Ct);
            Assert.NotEqual(0, await db.Notificacoes.CountAsync(Ct));
            Assert.NotEqual(0, await db.EventosSaida.CountAsync(Ct));
        }

        var antes = await RetratoDoQueFicaAsync(banco);

        var resultado = await PrepararAsync(api);

        Assert.True(resultado.Preparada, resultado.Mensagem);
        Assert.Equal(antes, await RetratoDoQueFicaAsync(banco));
        await using (var db = Banco(banco))
        {
            Assert.Equal(60, await db.Leads.CountAsync(Ct));
            Assert.False(await db.Leads.AnyAsync(l => antigos.Contains(l.Id) || l.Teste, Ct));
            Assert.Equal(0, await db.Notificacoes.CountAsync(Ct));
            Assert.Equal(0, await db.EventosSaida.CountAsync(Ct));
            Assert.Equal(0, await db.Importacoes.CountAsync(Ct));
            Assert.Equal(
                MassaDemonstracao.Campanhas.Select(c => c.Nome).Order(StringComparer.Ordinal),
                (await db.Campanhas.Select(c => c.Nome).ToListAsync(Ct)).Order(StringComparer.Ordinal));
            var registro = await db.Auditoria.SingleAsync(r => r.Acao == AcoesAuditoria.DemonstracaoPreparada, Ct);
            Assert.Null(registro.UsuarioId);
        }

        var painel = await (await admin.GetAsync("/api/painel", Ct)).LerJsonAsync();
        Assert.Equal((60, 65, 11, 8), (
            painel.GetProperty("leadsNovos").GetInt32(),
            painel.GetProperty("entradas").GetInt32(),
            painel.GetProperty("ganhos").GetInt32(),
            painel.GetProperty("perdas").GetProperty("leads").GetInt32()));
        Assert.Equal([13, 12, 9, 7, 11, 8], painel.GetProperty("situacaoAtual").EnumerateArray().Select(s => s.GetProperty("leadsNovos").GetInt32()));
        Assert.Equal(
            [("Adriana", 31), ("Pietra", 29)],
            painel.GetProperty("porAtendente").EnumerateArray()
                .Select(a => (a.GetProperty("responsavel").GetProperty("nome").GetString(), a.GetProperty("leadsNovos").GetInt32())));

        var usuarios = await (await admin.GetAsync("/api/usuarios", Ct)).LerJsonAsync();
        var proximo = Assert.Single(usuarios.EnumerateArray(), u => u.GetProperty("proximoDaVez").GetBoolean());
        Assert.Equal(adriana.Id, proximo.GetProperty("id").GetGuid());
    }

    /// <summary>Cada hora que a preparação gravou, com o que ela é, para a falha dizer qual passou de agora.</summary>
    private static async Task<List<(string Oque, DateTimeOffset Hora)>> HorasGravadasAsync(string banco)
    {
        await using var db = Banco(banco);
        var horas = new List<(string Oque, DateTimeOffset? Hora)>();
        foreach (var lead in await db.Leads.AsNoTracking().ToListAsync(Ct))
        {
            horas.Add(($"lead {lead.Id}: criado", lead.CriadoEm));
            horas.Add(($"lead {lead.Id}: atualizado", lead.AtualizadoEm));
            horas.Add(($"lead {lead.Id}: primeiro contato", lead.PrimeiroContatoEm));
            horas.Add(($"lead {lead.Id}: ganho", lead.ConvertidoEm));
            horas.Add(($"lead {lead.Id}: perda", lead.PerdidoEm));
        }

        foreach (var entrada in await db.EntradasRecebidas.AsNoTracking().ToListAsync(Ct))
        {
            horas.Add(($"entrada {entrada.Id}: recebida", entrada.RecebidoEm));
            horas.Add(($"entrada {entrada.Id}: processada", entrada.ProcessadoEm));
        }

        foreach (var interacao in await db.Interacoes.AsNoTracking().ToListAsync(Ct))
        {
            horas.Add(($"interação {interacao.Id} ({interacao.Tipo}): ocorrida", interacao.OcorridoEm));
            horas.Add(($"interação {interacao.Id} ({interacao.Tipo}): registrada", interacao.CriadoEm));
        }

        foreach (var mudanca in await db.HistoricoEtapas.AsNoTracking().ToListAsync(Ct))
        {
            horas.Add(($"mudança de etapa {mudanca.Id}", mudanca.OcorridoEm));
        }

        return [.. horas.Where(h => h.Hora is not null).Select(h => (h.Oque, h.Hora!.Value))];
    }

    [Theory]
    [InlineData("2026-10-08T11:00:00Z")] // 08:00 em Brasília
    [InlineData("2026-10-08T09:30:00Z")] // 06:30
    [InlineData("2026-10-08T03:10:00Z")] // 00:10
    [InlineData("2026-03-02T09:00:00Z")] // 06:00 de 02/03, meses antes: o teste não depende da data em que roda
    public async Task Preparar_de_manha_cedo_nao_deixa_nenhuma_hora_no_futuro(string instante)
    {
        var agora = DateTimeOffset.Parse(instante, CultureInfo.InvariantCulture);
        var banco = await fabrica.CriarBancoAsync("demonstracao");
        using var api = CriarApi(banco, relogio: new FakeTimeProvider(agora));
        await CriarAtendentesAsync(api, banco);

        Assert.True((await PrepararAsync(api)).Preparada);

        var horas = await HorasGravadasAsync(banco);
        Assert.NotEmpty(horas);
        Assert.Empty(horas.Where(h => h.Hora > agora).Select(h => $"{h.Oque} às {h.Hora:O}, depois de {agora:O}"));

        // O painel dos últimos 30 dias, contados a partir desse agora, pega todos. A sessão é aberta numa API com o
        // relógio de verdade: o cookie do login vence 12 horas depois do relógio da API, e o HttpClient descarta um
        // cookie que já venceu no relógio de verdade, o que fazia o teste falhar dependendo da hora em que rodava.
        var hoje = HorarioBrasilia.Dia(agora);
        using var apiDeVerdade = CriarApi(banco);
        using var admin = await apiDeVerdade.LogarComoAsync(Perfil.Admin);
        var resposta = await admin.GetAsync($"/api/painel?de={hoje.AddDays(-29):yyyy-MM-dd}&ate={hoje:yyyy-MM-dd}", Ct);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var painel = await resposta.LerJsonAsync();
        Assert.Equal((60, 65), (painel.GetProperty("leadsNovos").GetInt32(), painel.GetProperty("entradas").GetInt32()));
    }

    [Fact]
    public async Task Rodar_de_novo_depois_de_um_ensaio_volta_ao_mesmo_estado()
    {
        var banco = await fabrica.CriarBancoAsync("demonstracao");
        using var api = CriarApi(banco);
        var (adriana, _) = await CriarAtendentesAsync(api, banco);
        using var admin = await api.LogarComoAsync(Perfil.Admin);
        using var n8n = api.CriarClienteIntegracao();
        Assert.True((await PrepararAsync(api)).Preparada);
        var primeiraRodada = await NumerosDoPainelAsync(admin);

        // O ensaio: um lead novo pela integração, que vai para a Adriana e passa a vez para a Pietra.
        await n8n.EnviarLeadIdAsync(LeadsDeTeste.CorpoIntegracao());
        Assert.True((await PrepararAsync(api)).Preparada);

        Assert.Equal(primeiraRodada, await NumerosDoPainelAsync(admin));
        await using var db = Banco(banco);
        Assert.Equal(60, await db.Leads.CountAsync(Ct));
        Assert.Equal(4, await db.Campanhas.CountAsync(Ct));
        var usuarios = await (await admin.GetAsync("/api/usuarios", Ct)).LerJsonAsync();
        Assert.Equal(adriana.Id, Assert.Single(usuarios.EnumerateArray(), u => u.GetProperty("proximoDaVez").GetBoolean()).GetProperty("id").GetGuid());
    }
}
