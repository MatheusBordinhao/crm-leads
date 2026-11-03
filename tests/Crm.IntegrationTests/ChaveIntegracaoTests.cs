using System.Text.Json;
using Crm.Api.Features.Integracoes;
using Crm.Api.Features.Usuarios;
using Crm.IntegrationTests.Infra;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Crm.IntegrationTests;

/// <summary>
/// A chave da integração (seção 4.2) e a anterior, que vale junto com ela só durante a troca (README › Trocar chaves e
/// senhas). Cada caso chama as três rotas da integração pelo TestServer, com o <c>X-Api-Key</c> montado na mão: sem ele,
/// vazio, repetido ou com um valor. Assim, o caso do cabeçalho vazio chega mesmo vazio, e não como um cabeçalho ausente.
/// </summary>
public class ChaveIntegracaoTests(CrmApiFactory fabrica)
{
    private const string Atual = CrmApiFactory.ChaveIntegracao;

    private const string Anterior = "chave-anterior-da-integracao-dos-testes-9876543210";

    private const string OutraDoMesmoTamanho = "chave-errada-mas-com-o-mesmo-tamanho-0123";

    private const string RotaDosLeads = "/api/integracoes/leads";

    /// <summary>As três rotas da integração e a resposta de cada uma com uma chave que vale.</summary>
    private static readonly (string Metodo, string Caminho, int Sucesso)[] Rotas =
    [
        (HttpMethods.Post, RotaDosLeads, StatusCodes.Status201Created),
        (HttpMethods.Get, "/api/integracoes/relatorios/campanhas", StatusCodes.Status200OK),
        (HttpMethods.Get, "/api/integracoes/relatorios/campanhas/csv", StatusCodes.Status200OK),
    ];

    /// <summary>Os cabeçalhos que nunca valem, com ou sem a anterior. Null é sem o cabeçalho.</summary>
    private static readonly (string Caso, string[]? Cabecalho)[] SempreRecusados =
    [
        ("sem o cabeçalho", null),
        ("com o cabeçalho vazio", [""]),
        ("com outra chave do mesmo tamanho", [OutraDoMesmoTamanho]),
        ("com a atual sem o último caractere", [Atual[..^1]]),
        ("com a atual em maiúsculas", [Atual.ToUpperInvariant()]),
        ("com a atual e a anterior num cabeçalho só", [$"{Atual},{Anterior}"]),
    ];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Os_casos_passam_por_todas_as_rotas_da_integracao()
    {
        var daIntegracao = fabrica.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(a => a.Policy == Politicas.Integracao))
            .Select(e => (e.Metadata.GetRequiredMetadata<HttpMethodMetadata>().HttpMethods.Single(), "/" + e.RoutePattern.RawText))
            .Order();

        Assert.Equal(Rotas.Select(r => (r.Metodo, r.Caminho)).Order(), daIntegracao);
    }

    /// <summary>Sem a anterior, ausente como no .env de desenvolvimento ou vazia como no Compose de produção.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Sem_a_anterior_so_a_atual_vale(string? anterior)
    {
        using var api = Api(Atual, anterior);

        await AssertAceitaAsync(api, Atual, "com a atual");
        await AssertRecusaAsync(api, [.. SempreRecusados, ("com a chave de antes", [Anterior])]);
    }

    [Fact]
    public async Task Durante_a_troca_a_atual_e_a_anterior_valem()
    {
        using var api = Api(Atual, Anterior);

        await AssertAceitaAsync(api, Atual, "com a atual");
        await AssertAceitaAsync(api, Anterior, "com a anterior");
    }

    [Fact]
    public async Task Durante_a_troca_as_outras_chaves_dao_401()
    {
        using var api = Api(Atual, Anterior);

        await AssertRecusaAsync(
            api,
            [
                .. SempreRecusados,
                ("com a anterior sem o último caractere", [Anterior[..^1]]),
                ("com a anterior em maiúsculas", [Anterior.ToUpperInvariant()]),
                ("com a atual e a anterior em dois cabeçalhos", [Atual, Anterior]),
            ]);
    }

    /// <summary>Com a atual vazia, a integração fica desligada: nem o cabeçalho vazio vale contra a chave vazia.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Com_a_integracao_desligada_nenhuma_chave_vale(string? anterior)
    {
        using var api = Api("", anterior);

        await AssertRecusaAsync(api, [("com a chave de antes de desligar", [Atual]), ("com o cabeçalho vazio", [""]), ("sem o cabeçalho", null)]);
    }

    [Fact]
    public async Task O_uso_da_anterior_vai_para_o_log_sem_nenhuma_chave()
    {
        var captura = new CapturaDeLogs();
        using var api = Api(Atual, Anterior, captura);

        Assert.Equal(StatusCodes.Status201Created, await ChamarAsync(api, HttpMethods.Post, RotaDosLeads, [Anterior]));
        Assert.Equal(StatusCodes.Status201Created, await ChamarAsync(api, HttpMethods.Post, RotaDosLeads, [Atual]));
        Assert.Equal(StatusCodes.Status401Unauthorized, await ChamarAsync(api, HttpMethods.Post, RotaDosLeads, [OutraDoMesmoTamanho]));

        var registros = captura.Registros.ToList();
        Assert.Contains(registros, r => r.StartsWith("Trace ", StringComparison.Ordinal));
        Assert.Single(registros, r => r.Contains(ChaveIntegracaoHandler.AvisoUso, StringComparison.Ordinal));
        Assert.Single(registros, r => r.Contains(ChaveIntegracaoHandler.AvisoTroca, StringComparison.Ordinal));
        foreach (var chave in new[] { Atual, Anterior, OutraDoMesmoTamanho })
        {
            var vazados = registros.Where(r => r.Contains(chave, StringComparison.OrdinalIgnoreCase)).ToList();
            Assert.True(vazados.Count == 0, $"O log tem uma chave:{Environment.NewLine}{string.Join(Environment.NewLine, vazados)}");
        }
    }

    /// <summary>A anterior vazia é a da produção depois da fase 2: o aviso da subida precisa sumir.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Sem_a_anterior_nenhum_aviso_da_troca_vai_para_o_log(string? anterior)
    {
        var captura = new CapturaDeLogs();
        using var api = Api(Atual, anterior, captura);

        Assert.Equal(StatusCodes.Status201Created, await ChamarAsync(api, HttpMethods.Post, RotaDosLeads, [Atual]));
        Assert.Equal(StatusCodes.Status401Unauthorized, await ChamarAsync(api, HttpMethods.Post, RotaDosLeads, [Anterior]));

        var registros = captura.Registros.ToList();
        Assert.Contains(registros, r => r.StartsWith("Trace ", StringComparison.Ordinal));
        Assert.DoesNotContain(registros, r => r.Contains(ChaveIntegracaoHandler.AvisoUso, StringComparison.Ordinal));
        Assert.DoesNotContain(registros, r => r.Contains(ChaveIntegracaoHandler.AvisoTroca, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("chave-curta-da-integracao", null, "Integracao__Chave precisa ter pelo menos 32 caracteres")]
    [InlineData(Atual, "anterior-curta-da-integracao", "Integracao__ChaveAnterior precisa ter pelo menos 32 caracteres")]
    [InlineData("", Anterior, "Integracao__ChaveAnterior só vale com a Integracao__Chave preenchida")]
    [InlineData(Atual, Atual, "Integracao__ChaveAnterior não pode ser igual à Integracao__Chave")]
    public void Uma_chave_que_nao_serve_impede_a_API_de_subir_sem_mostrar_as_chaves(string atual, string? anterior, string mensagem)
    {
        using var api = Api(atual, anterior);

        var erro = Assert.ThrowsAny<Exception>(() => api.CreateClient()).ToString();

        Assert.Contains(mensagem, erro, StringComparison.Ordinal);
        foreach (var chave in new[] { atual, anterior }.Where(c => !string.IsNullOrEmpty(c)))
        {
            Assert.DoesNotContain(chave!, erro, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A API com a atual e a anterior do caso. A anterior null fica fora da configuração, como no .env de
    /// desenvolvimento, que ignora o valor vazio; a vazia é a do Compose de produção, que passa a variável sem valor.
    /// </summary>
    private WebApplicationFactory<Program> Api(string atual, string? anterior, CapturaDeLogs? logs = null) =>
        fabrica.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Integracao:Chave", atual);
            if (anterior is not null)
            {
                builder.UseSetting("Integracao:ChaveAnterior", anterior);
            }

            if (logs is not null)
            {
                builder.ConfigureLogging(logging => logging.AddProvider(logs).AddFilter<CapturaDeLogs>(_ => true));
            }
        });

    private static async Task AssertAceitaAsync(WebApplicationFactory<Program> api, string chave, string caso)
    {
        foreach (var (metodo, caminho, sucesso) in Rotas)
        {
            var status = await ChamarAsync(api, metodo, caminho, [chave]);
            Assert.True(status == sucesso, $"{metodo} {caminho} respondeu {status} {caso}");
        }
    }

    private static async Task AssertRecusaAsync(WebApplicationFactory<Program> api, IEnumerable<(string Caso, string[]? Cabecalho)> casos)
    {
        foreach (var (caso, cabecalho) in casos)
        {
            foreach (var (metodo, caminho, _) in Rotas)
            {
                var status = await ChamarAsync(api, metodo, caminho, cabecalho);
                Assert.True(status == StatusCodes.Status401Unauthorized, $"{metodo} {caminho} respondeu {status} {caso}");
            }
        }
    }

    /// <summary>Chama a rota com o <c>X-Api-Key</c> do caso: sem ele (null), vazio, com um valor ou repetido.</summary>
    private static async Task<int> ChamarAsync(WebApplicationFactory<Program> api, string metodo, string caminho, string[]? cabecalho)
    {
        var contexto = await api.Server.SendAsync(
            c =>
            {
                c.Request.Method = metodo;
                c.Request.Path = caminho;
                if (cabecalho is not null)
                {
                    c.Request.Headers[ChaveIntegracaoHandler.Cabecalho] = new StringValues(cabecalho);
                }

                if (HttpMethods.IsPost(metodo))
                {
                    c.Request.ContentType = "application/json";
                    c.Request.Body = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(LeadsDeTeste.CorpoIntegracao(), JsonSerializerOptions.Web));
                }
            },
            Ct);
        return contexto.Response.StatusCode;
    }
}
