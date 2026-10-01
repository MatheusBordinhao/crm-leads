using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Crm.Api.Features.Leads;
using Crm.Api.Infra.Dados;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests.Infra;

/// <summary>
/// Arquivos e chamadas da importação para os testes. Os arquivos imitam os dois casos da seção 3.7: o exportado pela
/// Meta (UTF-16 com tab) e o salvo pelo Excel brasileiro (Windows-1252 com ponto e vírgula). Cada linha usa e-mail,
/// telefone e ids únicos, porque o banco é compartilhado.
/// </summary>
public static class ImportacoesDeTeste
{
    public static readonly Encoding Windows1252 = CriarWindows1252();

    /// <summary>O cabeçalho da exportação de leads da Meta, com uma pergunta extra do formulário.</summary>
    public static readonly string[] CabecalhoMeta =
    [
        "id", "created_time", "ad_id", "ad_name", "adset_id", "adset_name", "campaign_id", "campaign_name", "form_id",
        "form_name", "is_organic", "platform", "full_name", "email", "phone_number", "company_name", "qual_serviço_você_procura?",
    ];

    /// <summary>Um cabeçalho de planilha montada à mão no Excel.</summary>
    public static readonly string[] CabecalhoExcel = ["Nome", "E-mail", "Celular", "Empresa", "Observações"];

    /// <summary>Um id numérico como os da Meta, único.</summary>
    public static string IdDaMeta() =>
        $"{RandomNumberGenerator.GetInt32(100_000, 1_000_000)}{RandomNumberGenerator.GetInt32(0, 1_000_000_000):D9}";

    /// <summary>Uma linha da Meta com os prefixos da exportação (<c>l:</c>, <c>c:</c>, <c>p:</c>...).</summary>
    public static string[] LinhaMeta(
        string nome,
        string? idLead = null,
        string? email = null,
        string? telefone = null,
        string? campanhaId = null,
        string empresa = "Açaí & Cia",
        string resposta = "Manutenção elétrica") =>
    [
        $"l:{idLead ?? IdDaMeta()}", "2026-09-28T14:03:11-03:00", "ag:120210000000003", "Vídeo 1", "as:120210000000002",
        "RS | 25-55", $"c:{campanhaId ?? IdDaMeta()}", "Outubro | Serviço Y", "f:987654321", "Orçamento", "false", "ig", nome,
        email ?? LeadsDeTeste.EmailUnico(), $"p:{telefone ?? LeadsDeTeste.TelefoneUnico()}", empresa, resposta,
    ];

    public static string[] LinhaExcel(string nome, string? email = null, string? celular = null, string empresa = "", string observacao = "") =>
        [nome, email ?? LeadsDeTeste.EmailUnico(), celular ?? LeadsDeTeste.ComoDigitado(LeadsDeTeste.TelefoneUnico()), empresa, observacao];

    /// <summary>O arquivo como a Meta exporta: UTF-16 little endian com BOM, tab e CRLF.</summary>
    public static byte[] ArquivoDaMeta(params string[][] linhas) =>
        [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(Texto('\t', [CabecalhoMeta, .. linhas]))];

    /// <summary>O arquivo como o Excel brasileiro salva: Windows-1252, ponto e vírgula e CRLF.</summary>
    public static byte[] ArquivoDoExcel(params string[][] linhas) => Windows1252.GetBytes(Texto(';', [CabecalhoExcel, .. linhas]));

    private static string Texto(char separador, IEnumerable<string[]> linhas) =>
        string.Concat(linhas.Select(l => string.Join(separador, l.Select(c => Campo(c, separador))) + "\r\n"));

    private static string Campo(string valor, char separador) =>
        valor.Contains(separador) || valor.Contains('"') || valor.Contains('\n')
            ? $"\"{valor.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : valor;

    public static Task<HttpResponseMessage> PostAnaliseAsync(this HttpClient admin, byte[] arquivo, string nomeArquivo = "leads.csv") =>
        admin.PostAsJsonAsync(
            "/api/importacoes/analise",
            new { nomeArquivo, conteudoBase64 = Convert.ToBase64String(arquivo) },
            TestContext.Current.CancellationToken);

    public static async Task<JsonElement> AnalisarAsync(this HttpClient admin, byte[] arquivo, string nomeArquivo = "leads.csv")
    {
        var resposta = await admin.PostAnaliseAsync(arquivo, nomeArquivo);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return await resposta.LerJsonAsync();
    }

    /// <summary>O corpo de <c>POST /api/importacoes</c> com o que a análise sugeriu. Os testes mudam o que precisam.</summary>
    public static Dictionary<string, object?> CorpoImportacao(byte[] arquivo, JsonElement analise, string nomeArquivo = "leads.csv") => new()
    {
        ["nomeArquivo"] = nomeArquivo,
        ["conteudoBase64"] = Convert.ToBase64String(arquivo),
        ["campos"] = analise.GetProperty("colunas").EnumerateArray().Select(c => c.GetProperty("sugestao").GetString()).ToList(),
        ["colunaIdExterno"] = analise.GetProperty("colunaIdExterno").ValueKind == JsonValueKind.Null
            ? null
            : analise.GetProperty("colunaIdExterno").GetInt32(),
        ["origem"] = analise.GetProperty("origem").GetString(),
        ["campanhaPadraoId"] = null,
        ["novaCampanha"] = null,
        ["responsavelId"] = null,
    };

    public static Task<HttpResponseMessage> PostImportacaoAsync(this HttpClient admin, object corpo) =>
        admin.PostAsJsonAsync("/api/importacoes", corpo, TestContext.Current.CancellationToken);

    /// <summary>Analisa, importa com a sugestão (mais as mudanças) e espera a importação terminar.</summary>
    public static async Task<JsonElement> ImportarAsync(
        this HttpClient admin, byte[] arquivo, Action<Dictionary<string, object?>>? ajustar = null, string nomeArquivo = "leads.csv")
    {
        var corpo = CorpoImportacao(arquivo, await admin.AnalisarAsync(arquivo, nomeArquivo), nomeArquivo);
        ajustar?.Invoke(corpo);
        var resposta = await admin.PostImportacaoAsync(corpo);
        Assert.True(resposta.StatusCode == HttpStatusCode.Accepted, await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return await admin.AguardarConclusaoAsync((await resposta.LerJsonAsync()).GetProperty("id").GetInt64());
    }

    /// <summary>Consulta a importação até ela terminar, como a tela faz.</summary>
    public static async Task<JsonElement> AguardarConclusaoAsync(this HttpClient admin, long importacaoId)
    {
        var limite = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            var resposta = await admin.GetAsync($"/api/importacoes/{importacaoId}", TestContext.Current.CancellationToken);
            resposta.EnsureSuccessStatusCode();
            var importacao = await resposta.LerJsonAsync();
            if (importacao.GetProperty("situacao").GetString() == "concluida")
            {
                return importacao;
            }

            Assert.True(DateTime.UtcNow < limite, $"A importação {importacaoId} não terminou em 30 segundos.");
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Os leads que a importação criou ou a que vinculou entradas, com a etapa e a campanha.</summary>
    public static async Task<List<Lead>> LeadsDaImportacaoAsync(this WebApplicationFactory<Program> fabrica, long importacaoId)
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<CrmDbContext>();
        var ids = db.EntradasRecebidas.Where(e => e.ImportacaoId == importacaoId).Select(e => e.LeadId);
        return await db.Leads
            .AsNoTracking()
            .Include(l => l.Etapa)
            .Include(l => l.Campanha)
            .Where(l => ids.Contains(l.Id))
            .OrderBy(l => l.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>As entradas que a importação gerou.</summary>
    public static async Task<List<EntradaRecebida>> EntradasDaImportacaoAsync(this WebApplicationFactory<Program> fabrica, long importacaoId)
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().EntradasRecebidas
            .AsNoTracking()
            .Where(e => e.ImportacaoId == importacaoId)
            .OrderBy(e => e.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Os leads com esse e-mail.</summary>
    public static async Task<List<long>> LeadsComEmailAsync(this WebApplicationFactory<Program> fabrica, string email)
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().Leads
            .Where(l => l.Email == email)
            .Select(l => l.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private static Encoding CriarWindows1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }
}
