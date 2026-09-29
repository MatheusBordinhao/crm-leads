using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Crm.Api.Features.Funil;
using Crm.Api.Features.Leads;
using Crm.Api.Infra.Dados;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests.Infra;

/// <summary>
/// Leads e dados pessoais para os testes. O banco é compartilhado, então cada teste usa e-mail, telefone e
/// documento únicos: senão, a deduplicação juntaria leads de testes diferentes.
/// </summary>
public static class LeadsDeTeste
{
    public static string EmailUnico() => $"lead-{Guid.NewGuid():N}@example.com";

    /// <summary>Celular de Porto Alegre em E.164, válido para o libphonenumber.</summary>
    public static string TelefoneUnico() => $"+555199{RandomNumberGenerator.GetInt32(0, 10_000_000):D7}";

    /// <summary>CPF válido, com os dígitos verificadores calculados.</summary>
    public static string CpfUnico()
    {
        var digitos = Enumerable.Range(0, 9).Select(_ => RandomNumberGenerator.GetInt32(0, 10)).ToList();
        digitos.Add(DigitoCpf(digitos));
        digitos.Add(DigitoCpf(digitos));
        return string.Concat(digitos);
    }

    public static string IdExternoUnico() => Guid.NewGuid().ToString("N");

    /// <summary>Entrada da Meta com e-mail e id externo únicos.</summary>
    public static NovaEntrada Entrada(Origem origem = Origem.MetaLeadAds, string nome = "Maria Souza") => new()
    {
        Origem = origem,
        IdExterno = IdExternoUnico(),
        RecebidoEm = DateTimeOffset.UtcNow,
        Nome = nome,
        Email = EmailUnico(),
    };

    /// <summary>Cliente do n8n: sem cookie, com a chave de integração.</summary>
    public static HttpClient CriarClienteIntegracao(this WebApplicationFactory<Program> fabrica, string? chave = CrmApiFactory.ChaveIntegracao)
    {
        var cliente = fabrica.CriarClienteHttps();
        if (chave is not null)
        {
            cliente.DefaultRequestHeaders.Add("X-Api-Key", chave);
        }

        return cliente;
    }

    /// <summary>Corpo da seção 4.2, com id externo e e-mail únicos. Os testes mudam ou tiram campos.</summary>
    public static Dictionary<string, object?> CorpoIntegracao(string origem = "meta_lead_ads") => new()
    {
        ["origem"] = origem,
        ["idExterno"] = IdExternoUnico(),
        ["recebidoEm"] = "2026-10-05T14:03:00Z",
        ["teste"] = false,
        ["nome"] = "Maria Souza",
        ["email"] = EmailUnico(),
        ["telefone"] = TelefoneUnico(),
        ["cpfCnpj"] = null,
        ["uf"] = "RS",
        ["empresa"] = "Empresa X",
        ["plataforma"] = "instagram",
        ["campanha"] = new { id = IdExternoUnico(), nome = "Outubro | Serviço Y" },
        ["conjunto"] = new { id = "120210000000002", nome = "RS | 25-55" },
        ["anuncio"] = new { id = "120210000000003", nome = "Vídeo 1" },
        ["formulario"] = new { id = "987654321", nome = "Orçamento" },
        ["utm"] = new { source = "instagram", medium = "paid", campaign = "outubro-servico-y", content = (string?)null, term = (string?)null },
        ["gclid"] = null,
        ["respostasExtras"] = new Dictionary<string, string> { ["Qual serviço você procura?"] = "Serviço Y" },
    };

    public static Task<HttpResponseMessage> EnviarLeadAsync(this HttpClient cliente, object corpo) =>
        cliente.PostAsJsonAsync("/api/integracoes/leads", corpo, TestContext.Current.CancellationToken);

    /// <summary>Envia pela integração e devolve o id do lead.</summary>
    public static async Task<long> EnviarLeadIdAsync(this HttpClient cliente, Dictionary<string, object?> corpo)
    {
        var resposta = await cliente.EnviarLeadAsync(corpo);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.LerJsonAsync()).GetProperty("leadId").GetInt64();
    }

    /// <summary>Corpo do cadastro manual (<c>POST /api/leads</c>), com e-mail único.</summary>
    public static Dictionary<string, object?> CorpoLead(string nome = "Maria Souza") => new()
    {
        ["nome"] = nome,
        ["email"] = EmailUnico(),
        ["telefone"] = null,
        ["cpfCnpj"] = null,
        ["uf"] = null,
        ["empresa"] = null,
        ["origem"] = "manual",
        ["responsavelId"] = null,
    };

    public static Task<HttpResponseMessage> CriarLeadAsync(this HttpClient cliente, object corpo) =>
        cliente.PostAsJsonAsync("/api/leads", corpo, TestContext.Current.CancellationToken);

    /// <summary>Cadastra pela tela e devolve o id do lead.</summary>
    public static async Task<long> CriarLeadIdAsync(this HttpClient cliente, Dictionary<string, object?>? corpo = null)
    {
        var resposta = await cliente.CriarLeadAsync(corpo ?? CorpoLead());
        resposta.EnsureSuccessStatusCode();
        return (await resposta.LerJsonAsync()).GetProperty("leadId").GetInt64();
    }

    /// <summary>Telefone em E.164 escrito como a pessoa digita: <c>(51) 99123-4567</c>.</summary>
    public static string ComoDigitado(string e164) => $"({e164[3..5]}) {e164[5..10]}-{e164[10..]}";

    /// <summary>Processa a entrada pelo serviço, num escopo próprio, como numa requisição.</summary>
    public static async Task<EntradaProcessada> ReceberAsync(this WebApplicationFactory<Program> fabrica, NovaEntrada entrada)
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<ReceberEntrada>().ExecutarAsync(entrada, TestContext.Current.CancellationToken);
    }

    public static async Task<Lead> BuscarLeadAsync(this WebApplicationFactory<Program> fabrica, long id)
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().Leads
            .AsNoTracking()
            .Include(l => l.Etapa)
            .Include(l => l.Campanha)
            .SingleAsync(l => l.Id == id, TestContext.Current.CancellationToken);
    }

    public static Task<HttpResponseMessage> MudarEtapaAsync(
        this HttpClient cliente, long leadId, int etapaId, int? motivoPerdaId = null, string? motivoPerdaDetalhe = null) =>
        cliente.PostAsJsonAsync(
            $"/api/leads/{leadId}/etapa", new { etapaId, motivoPerdaId, motivoPerdaDetalhe }, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> RegistrarInteracaoAsync(
        this HttpClient cliente, long leadId, string tipo, string? descricao = null, DateTimeOffset? ocorridoEm = null) =>
        cliente.PostAsJsonAsync(
            $"/api/leads/{leadId}/interacoes",
            new { tipo, descricao, ocorridoEm = ocorridoEm ?? DateTimeOffset.UtcNow },
            TestContext.Current.CancellationToken);

    /// <summary>Os itens da linha do tempo, do mais recente para o mais antigo.</summary>
    public static async Task<List<JsonElement>> LinhaDoTempoAsync(this HttpClient cliente, long leadId)
    {
        var resposta = await cliente.GetAsync($"/api/leads/{leadId}/linha-do-tempo", TestContext.Current.CancellationToken);
        resposta.EnsureSuccessStatusCode();
        return [.. (await resposta.LerJsonAsync()).EnumerateArray()];
    }

    /// <summary>Uma das etapas iniciais (seção 3.4), pelo nome. Os testes não renomeiam essas etapas.</summary>
    public static async Task<Etapa> EtapaAsync(this WebApplicationFactory<Program> fabrica, string nome)
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().Etapas
            .AsNoTracking()
            .SingleAsync(e => e.Nome == nome, TestContext.Current.CancellationToken);
    }

    /// <summary>Um motivo de perda da lista inicial, pelo nome.</summary>
    public static async Task<int> MotivoPerdaIdAsync(this WebApplicationFactory<Program> fabrica, string nome = "Sem resposta")
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().MotivosPerda
            .Where(m => m.Nome == nome)
            .Select(m => m.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>As mudanças de etapa do lead, da primeira para a última.</summary>
    public static async Task<List<HistoricoEtapa>> HistoricoAsync(this WebApplicationFactory<Program> fabrica, long leadId)
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().HistoricoEtapas
            .AsNoTracking()
            .Where(h => h.LeadId == leadId)
            .OrderBy(h => h.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Muda a etapa direto no banco, como se alguém tivesse mudado pela tela.</summary>
    public static async Task MoverParaAsync(this WebApplicationFactory<Program> fabrica, long leadId, TipoEtapa tipo)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<CrmDbContext>();
        var lead = await db.Leads.SingleAsync(l => l.Id == leadId, ct);
        lead.EtapaId = await db.Etapas.Where(e => e.Ativa && e.Tipo == tipo).OrderBy(e => e.Ordem).Select(e => e.Id).FirstAsync(ct);
        if (tipo == TipoEtapa.Perdido)
        {
            lead.PerdidoEm = DateTimeOffset.UtcNow;
            lead.MotivoPerdaId = await db.MotivosPerda.Select(m => m.Id).FirstAsync(ct);
            lead.MotivoPerdaDetalhe = "Não respondeu";
        }
        else if (tipo == TipoEtapa.Ganho)
        {
            lead.ConvertidoEm = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    private static int DigitoCpf(List<int> digitos)
    {
        var soma = digitos.Select((d, i) => d * (digitos.Count + 1 - i)).Sum();
        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}
