using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.Features.Funil;
using Crm.Api.Features.Leads;
using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Dados;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests.Infra;

/// <summary>
/// Leads e dados pessoais para os testes. O banco é compartilhado, então cada teste usa e-mail, telefone e
/// documento únicos: senão, a deduplicação juntaria leads de testes diferentes, e a busca acharia os de outro teste.
/// O telefone, o CPF e a marca de busca vêm de um contador do processo, e não de um sorteio, que pode repetir.
/// </summary>
public static class LeadsDeTeste
{
    private static int _sequencia;

    /// <summary>
    /// Um número que nenhuma outra chamada recebe nesta rodada dos testes. O contador recomeça a cada rodada, o que só vale
    /// porque o banco também nasce vazio: a <see cref="CrmApiFactory"/> confere.
    /// </summary>
    public static int NumeroUnico() => Interlocked.Increment(ref _sequencia);

    public static string EmailUnico() => $"lead-{Guid.NewGuid():N}@example.com";

    /// <summary>
    /// Celular de Porto Alegre em E.164, válido para o libphonenumber: <c>+5551990000001</c>, <c>+5551990000002</c>... Os
    /// telefones fixos dos testes, como <c>+5551999998888</c>, ficam bem longe dessa faixa.
    /// </summary>
    public static string TelefoneUnico() => $"+555199{NumeroUnico():D7}";

    /// <summary>
    /// CPF válido, com os dígitos verificadores calculados sobre a base <c>100000001</c>, <c>100000002</c>... Começa com
    /// 1, então nunca aparece dentro de um telefone dos testes, que começam com 55, nem o contrário.
    /// </summary>
    public static string CpfUnico()
    {
        var digitos = (100_000_000 + NumeroUnico()).ToString(CultureInfo.InvariantCulture).Select(c => c - '0').ToList();
        digitos.Add(DigitoCpf(digitos));
        digitos.Add(DigitoCpf(digitos));
        return string.Concat(digitos);
    }

    /// <summary>
    /// Texto único para achar na busca os leads de um teste: <c>zq</c> e mais 8 letras. Nenhum e-mail dos testes tem
    /// "zq", porque eles usam hexadecimal, e a marca não tem algarismos: com 4 ou mais, a busca procura também no telefone
    /// e no CPF/CNPJ de todos os leads e acharia os de outros testes.
    /// </summary>
    public static string Marca()
    {
        var numero = NumeroUnico();
        var letras = new char[8];
        for (var posicao = letras.Length - 1; posicao >= 0; posicao--)
        {
            letras[posicao] = (char)('a' + (numero % 26));
            numero /= 26;
        }

        return "zq" + new string(letras);
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

    public static Task<HttpResponseMessage> AnonimizarAsync(this HttpClient cliente, long leadId) =>
        cliente.PostAsync($"/api/leads/{leadId}/anonimizar", null, TestContext.Current.CancellationToken);

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

    /// <summary>Cria uma etapa aberta com nome único e devolve a resposta (id, nome, ordem, tipo, ativa).</summary>
    public static async Task<JsonElement> CriarEtapaAsync(this HttpClient admin, string? nome = null)
    {
        var resposta = await admin.PostAsJsonAsync(
            "/api/etapas", new { nome = nome ?? $"Etapa {Guid.NewGuid():N}"[..30] }, TestContext.Current.CancellationToken);
        resposta.EnsureSuccessStatusCode();
        return await resposta.LerJsonAsync();
    }

    /// <summary>Os registros da auditoria de uma entidade, do mais antigo para o mais recente.</summary>
    public static async Task<List<RegistroAuditoria>> AuditoriaAsync(this WebApplicationFactory<Program> fabrica, string entidade, string entidadeId)
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().Auditoria
            .AsNoTracking()
            .Where(r => r.Entidade == entidade && r.EntidadeId == entidadeId)
            .OrderBy(r => r.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
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
