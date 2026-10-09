using System.Net;
using System.Text.Json;
using Crm.Api.Features.Notificacoes;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.Eventos;
using Crm.IntegrationTests.Infra;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Crm.IntegrationTests;

/// <summary>
/// Despachante dos eventos para o n8n (seção 4.3). Ele lê a fila inteira de <c>eventos_saida</c>, então estes testes
/// usam um banco só deles, no mesmo contêiner, e um relógio falso, para as esperas passarem sem esperar de verdade.
/// </summary>
public class DespachanteEventosTests(CrmApiFactory fabrica)
{
    private const string UrlDoN8n = "https://n8n.teste/webhook/crm-alertas";

    private const string ChaveDoN8n = "chave-do-webhook-do-n8n-nos-testes-0123456789";

    private static readonly SemaphoreSlim CriacaoDoBanco = new(1, 1);

    private static string? _banco;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Entrega_o_evento_com_a_chave_o_id_e_o_json_e_marca_como_entregue()
    {
        var n8n = new N8nFalso();
        await using var api = await CriarApiAsync(n8n);
        var atendente = await api.Fabrica.CriarUsuarioAsync();
        var evento = await api.CriarEventoAsync(atendente.Id);

        var tentados = await api.DespacharAsync();

        Assert.Equal(1, tentados);
        var requisicao = Assert.Single(n8n.Recebidas);
        Assert.Equal(HttpMethod.Post, requisicao.Metodo);
        Assert.Equal(UrlDoN8n, requisicao.Url.ToString());
        Assert.Equal(ChaveDoN8n, requisicao.Chave);
        Assert.Equal(evento.Id.ToString(), requisicao.EventoId);
        Assert.Equal("application/json", requisicao.TipoDoConteudo);
        var corpo = JsonSerializer.Deserialize<JsonElement>(requisicao.Corpo);
        Assert.Equal(evento.Id, corpo.GetProperty("id").GetGuid());
        Assert.Equal("lead.criado", corpo.GetProperty("tipo").GetString());
        Assert.Equal($"{CrmApiFactory.UrlPublica}/leads/{evento.LeadId}", corpo.GetProperty("lead").GetProperty("url").GetString());
        Assert.Equal(atendente.Email, corpo.GetProperty("responsavel").GetProperty("email").GetString());

        var entregue = await api.EventoAsync(evento.Id);
        Assert.Equal(SituacaoEvento.Entregue, entregue.Situacao);
        Assert.Equal(1, entregue.Tentativas);
        Assert.Equal(api.Relogio.GetUtcNow(), entregue.EntregueEm);
        Assert.Null(entregue.UltimoErro);
        Assert.Equal(0, await api.DespacharAsync());
    }

    [Fact]
    public async Task Falha_deixa_o_evento_pendente_para_tentar_de_novo_em_1_minuto()
    {
        var n8n = new N8nFalso { Status = HttpStatusCode.ServiceUnavailable };
        await using var api = await CriarApiAsync(n8n);
        var evento = await api.CriarEventoAsync();
        var inicio = api.Relogio.GetUtcNow();

        await api.DespacharAsync();
        var depois = await api.EventoAsync(evento.Id);
        api.Relogio.Advance(TimeSpan.FromSeconds(59));
        var antesDoMinuto = await api.DespacharAsync();
        api.Relogio.Advance(TimeSpan.FromSeconds(1));
        var noMinuto = await api.DespacharAsync();

        Assert.Equal(SituacaoEvento.Pendente, depois.Situacao);
        Assert.Equal(1, depois.Tentativas);
        Assert.Equal(inicio + TimeSpan.FromMinutes(1), depois.ProximaTentativaEm);
        Assert.Equal("HTTP 503", depois.UltimoErro);
        Assert.Equal(0, antesDoMinuto);
        Assert.Equal(1, noMinuto);
        Assert.Equal(2, (await api.EventoAsync(evento.Id)).Tentativas);
    }

    [Fact]
    public async Task Depois_da_ultima_tentativa_o_evento_falha_de_vez_e_os_admins_sao_avisados()
    {
        var n8n = new N8nFalso { Status = HttpStatusCode.InternalServerError };
        await using var api = await CriarApiAsync(n8n);
        var admin = await api.Fabrica.CriarUsuarioAsync(Perfil.Admin);
        var atendente = await api.Fabrica.CriarUsuarioAsync();
        var evento = await api.CriarEventoAsync(atendente.Id);

        var esperas = new List<TimeSpan>();
        for (var tentativa = 1; tentativa <= RegrasDespacho.MaximoDeTentativas; tentativa++)
        {
            var agora = api.Relogio.GetUtcNow();
            Assert.Equal(1, await api.DespacharAsync());
            var depois = await api.EventoAsync(evento.Id);
            Assert.Equal(tentativa, depois.Tentativas);
            if (depois.Situacao == SituacaoEvento.Pendente)
            {
                esperas.Add(depois.ProximaTentativaEm - agora);
                api.Relogio.SetUtcNow(depois.ProximaTentativaEm);
            }
        }

        Assert.Equal(RegrasDespacho.Esperas, esperas);
        var falhou = await api.EventoAsync(evento.Id);
        Assert.Equal(SituacaoEvento.Falhou, falhou.Situacao);
        Assert.Equal("HTTP 500", falhou.UltimoErro);
        api.Relogio.Advance(TimeSpan.FromDays(1));
        Assert.Equal(0, await api.DespacharAsync());
        Assert.Equal(RegrasDespacho.MaximoDeTentativas, n8n.Recebidas.Count);

        var aviso = Assert.Single(
            await api.Fabrica.NotificacoesDoUsuarioAsync(admin.Id, evento.LeadId), n => n.Tipo == TipoNotificacao.EventoFalhou);
        Assert.Equal("O aviso lead.criado não chegou ao n8n depois de 7 tentativas (HTTP 500)", aviso.Titulo);
        Assert.DoesNotContain(
            await api.Fabrica.NotificacoesDoUsuarioAsync(atendente.Id, evento.LeadId), n => n.Tipo == TipoNotificacao.EventoFalhou);
    }

    [Fact]
    public async Task Tempo_esgotado_e_n8n_fora_do_ar_contam_como_falha()
    {
        var n8n = new N8nFalso { Demorar = true };
        await using var api = await CriarApiAsync(n8n);
        var demorado = await api.CriarEventoAsync();

        await api.DespacharAsync();
        n8n.Demorar = false;
        n8n.ForaDoAr = true;
        var semConexao = await api.CriarEventoAsync();
        await api.DespacharAsync();

        var porTempo = await api.EventoAsync(demorado.Id);
        Assert.Equal(SituacaoEvento.Pendente, porTempo.Situacao);
        Assert.Equal("Tempo esgotado (1 s)", porTempo.UltimoErro);
        Assert.Equal("Falha de conexão (ConnectionError)", (await api.EventoAsync(semConexao.Id)).UltimoErro);
    }

    [Fact]
    public async Task O_erro_gravado_nao_repete_o_que_o_n8n_respondeu()
    {
        var marca = Guid.NewGuid().ToString("N")[..10];
        var n8n = new N8nFalso { Status = HttpStatusCode.BadRequest, CorpoDaResposta = $$"""{ "recebido": "Nome Sigiloso {{marca}}" }""" };
        await using var api = await CriarApiAsync(n8n);
        var evento = await api.CriarEventoAsync(nome: $"Nome Sigiloso {marca}");

        await api.DespacharAsync();

        Assert.Equal("HTTP 400", (await api.EventoAsync(evento.Id)).UltimoErro);
    }

    [Fact]
    public async Task Evento_apagado_durante_o_envio_fica_de_lado_e_a_rodada_continua()
    {
        // Na última tentativa e com o n8n respondendo erro: se ainda existisse, o evento falharia de vez e avisaria os admins.
        var n8n = new N8nFalso { Status = HttpStatusCode.InternalServerError };
        await using var api = await CriarApiAsync(n8n);
        var apagado = await api.CriarEventoAsync();
        var outro = await api.CriarEventoAsync();
        var admin = await api.Fabrica.CriarUsuarioAsync(Perfil.Admin);
        await api.DeixarNaUltimaTentativaAsync(apagado.Id);

        // A anonimização do lead apaga o evento enquanto ele está a caminho do n8n.
        n8n.AoReceber = async requisicao =>
        {
            if (requisicao.EventoId == apagado.Id.ToString())
            {
                await api.ApagarAsync(apagado.Id);
            }
        };

        var tentados = await api.DespacharAsync();

        Assert.Equal(2, tentados);
        Assert.Null(await api.EventoOuNuloAsync(apagado.Id));
        Assert.DoesNotContain(await api.Fabrica.NotificacoesDoUsuarioAsync(admin.Id), n => n.Tipo == TipoNotificacao.EventoFalhou);
        var seguinte = await api.EventoAsync(outro.Id);
        Assert.Equal((SituacaoEvento.Pendente, 1, "HTTP 500"), (seguinte.Situacao, seguinte.Tentativas, seguinte.UltimoErro));
    }

    [Fact]
    public async Task Sem_N8n__WebhookUrl_nada_e_enviado_e_o_evento_fica_pendente()
    {
        var n8n = new N8nFalso();
        await using var api = await CriarApiAsync(n8n, comUrl: false);
        var evento = await api.CriarEventoAsync();

        var tentados = await api.DespacharAsync();

        Assert.Equal(0, tentados);
        Assert.Empty(n8n.Recebidas);
        var pendente = await api.EventoAsync(evento.Id);
        Assert.Equal(SituacaoEvento.Pendente, pendente.Situacao);
        Assert.Equal(0, pendente.Tentativas);
    }

    [Fact]
    public async Task Com_o_n8n_fora_do_ar_o_evento_fica_pendente_e_e_entregue_quando_ele_volta()
    {
        // O serviço em segundo plano de verdade: ele confere a fila a cada 5 s do relógio falso.
        var n8n = new N8nFalso { ForaDoAr = true };
        await using var api = await CriarApiAsync(n8n, comServico: true);
        var evento = await api.CriarEventoAsync();

        api.Relogio.Advance(TimeSpan.FromSeconds(5));
        var pendente = await api.AguardarAsync(evento.Id, e => e.Tentativas >= 1);
        n8n.ForaDoAr = false;
        api.Relogio.Advance(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(5));
        var entregue = await api.AguardarAsync(evento.Id, e => e.Situacao == SituacaoEvento.Entregue);

        Assert.Equal(SituacaoEvento.Pendente, pendente.Situacao);
        Assert.Equal("Falha de conexão (ConnectionError)", pendente.UltimoErro);
        Assert.Equal(2, entregue.Tentativas);
        Assert.Equal(2, n8n.Recebidas.Count(r => r.EventoId == evento.Id.ToString()));
    }

    [Fact]
    public async Task O_despacho_nao_poe_dados_pessoais_nem_a_chave_no_log()
    {
        var marca = Guid.NewGuid().ToString("N")[..10];
        var captura = new CapturaDeLogs();
        var n8n = new N8nFalso { Status = HttpStatusCode.InternalServerError, CorpoDaResposta = $"Resposta sigilosa {marca}" };
        await using var api = await CriarApiAsync(n8n, logs: captura);
        var evento = await api.CriarEventoAsync(nome: $"Nome Sigiloso {marca}");
        var email = (await api.Fabrica.BuscarLeadAsync(evento.LeadId!.Value)).Email!;

        await api.DespacharAsync();
        n8n.Status = HttpStatusCode.OK;
        api.Relogio.Advance(TimeSpan.FromMinutes(1));
        await api.DespacharAsync();

        Assert.Equal(SituacaoEvento.Entregue, (await api.EventoAsync(evento.Id)).Situacao);
        var registros = captura.Registros.ToList();
        Assert.Contains(registros, r => r.StartsWith("Trace ", StringComparison.Ordinal));
        foreach (var sensivel in new[] { marca, email, ChaveDoN8n })
        {
            var vazados = registros.Where(r => r.Contains(sensivel, StringComparison.OrdinalIgnoreCase)).ToList();
            Assert.True(vazados.Count == 0, $"O log tem um dado pessoal ou secreto:{Environment.NewLine}{string.Join(Environment.NewLine, vazados)}");
        }
    }

    /// <summary>
    /// A API no banco do despachante, com o relógio falso e o n8n falso. Sem o serviço em segundo plano, o teste chama
    /// o despachante quando quer. A fila começa vazia.
    /// </summary>
    private async Task<ApiDoDespachante> CriarApiAsync(
        N8nFalso n8n, bool comUrl = true, bool comServico = false, CapturaDeLogs? logs = null)
    {
        var banco = await BancoAsync();
        await using (var db = new CrmDbContext(DadosExtensions.CriarOpcoes(banco)))
        {
            await db.EventosSaida.ExecuteDeleteAsync(Ct);
        }

        // Em segundos inteiros: o banco guarda até o microssegundo, e as comparações de hora ficam exatas.
        var agora = DateTimeOffset.UtcNow;
        var relogio = new FakeTimeProvider(agora.AddTicks(-(agora.Ticks % TimeSpan.TicksPerSecond)));
        var api = fabrica.WithWebHostBuilder(builder =>
        {
            builder.UseSetting($"ConnectionStrings:{DadosExtensions.NomeConexao}", banco);
            builder.UseSetting("N8n:WebhookUrl", comUrl ? UrlDoN8n : "");
            builder.UseSetting("N8n:WebhookChave", comUrl ? ChaveDoN8n : "");
            builder.UseSetting("N8n:TempoLimiteSegundos", "1");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<TimeProvider>(relogio);
                services.AddHttpClient(DespachanteEventos.NomeCliente).ConfigurePrimaryHttpMessageHandler(() => n8n);
                if (!comServico)
                {
                    services.Remove(services.Single(s => s.ImplementationType == typeof(ServicoDespacho)));
                }
            });
            if (logs is not null)
            {
                builder.ConfigureLogging(logging => logging.AddProvider(logs).AddFilter<CapturaDeLogs>(_ => true));
            }
        });

        return new ApiDoDespachante(api, relogio);
    }

    /// <summary>O banco do despachante, criado uma vez para a classe.</summary>
    private async Task<string> BancoAsync()
    {
        await CriacaoDoBanco.WaitAsync(Ct);
        try
        {
            return _banco ??= await fabrica.CriarBancoAsync("despachante");
        }
        finally
        {
            CriacaoDoBanco.Release();
        }
    }

    private sealed class ApiDoDespachante(WebApplicationFactory<Program> fabrica, FakeTimeProvider relogio) : IAsyncDisposable
    {
        public WebApplicationFactory<Program> Fabrica => fabrica;

        public FakeTimeProvider Relogio => relogio;

        public async Task<int> DespacharAsync()
        {
            await using var escopo = fabrica.Services.CreateAsyncScope();
            return await escopo.ServiceProvider.GetRequiredService<DespachanteEventos>().DespacharAsync(Ct);
        }

        /// <summary>Um lead novo pelo serviço de entrada, que grava o <c>lead.criado</c>.</summary>
        public async Task<EventoSaida> CriarEventoAsync(Guid? responsavelId = null, string nome = "Maria Souza")
        {
            var entrada = await fabrica.ReceberAsync(LeadsDeTeste.Entrada(nome: nome) with { ResponsavelId = responsavelId });
            return Assert.Single(await fabrica.EventosDoLeadAsync(entrada.LeadId));
        }

        public async Task<EventoSaida> EventoAsync(Guid id) => (await EventoOuNuloAsync(id))!;

        public async Task<EventoSaida?> EventoOuNuloAsync(Guid id)
        {
            await using var escopo = fabrica.Services.CreateAsyncScope();
            return await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().EventosSaida
                .AsNoTracking()
                .SingleOrDefaultAsync(e => e.Id == id, Ct);
        }

        /// <summary>Como se as outras tentativas já tivessem falhado: o próximo envio é o último.</summary>
        public async Task DeixarNaUltimaTentativaAsync(Guid id)
        {
            await using var escopo = fabrica.Services.CreateAsyncScope();
            await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().EventosSaida
                .Where(e => e.Id == id)
                .ExecuteUpdateAsync(e => e.SetProperty(x => x.Tentativas, RegrasDespacho.MaximoDeTentativas - 1), Ct);
        }

        public async Task ApagarAsync(Guid id)
        {
            await using var escopo = fabrica.Services.CreateAsyncScope();
            await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().EventosSaida.Where(e => e.Id == id).ExecuteDeleteAsync(Ct);
        }

        /// <summary>Espera, em tempo real, o serviço em segundo plano deixar o evento como o teste quer.</summary>
        public async Task<EventoSaida> AguardarAsync(Guid id, Func<EventoSaida, bool> condicao)
        {
            var limite = DateTime.UtcNow.AddSeconds(15);
            while (true)
            {
                var evento = await EventoAsync(id);
                if (condicao(evento))
                {
                    return evento;
                }

                Assert.True(DateTime.UtcNow < limite, $"O evento continuou {evento.Situacao}, com {evento.Tentativas} tentativas.");
                await Task.Delay(50, Ct);
            }
        }

        public ValueTask DisposeAsync() => fabrica.DisposeAsync();
    }
}
