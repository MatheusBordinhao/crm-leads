using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Crm.UnitTests.Infra;

/// <summary>Os arquivos da publicação (Passo 5): segredos fora da imagem e do Compose, o proxy certo e o Caddy.</summary>
public class PublicacaoTests
{
    private static readonly string Raiz = AcharRaiz();

    private static string Ler(string arquivo) => File.ReadAllText(Path.Combine(Raiz, arquivo));

    private static List<string> Linhas(string arquivo) => [.. Ler(arquivo).Split('\n').Select(l => l.TrimEnd('\r'))];

    [Fact]
    public void O_env_nunca_vai_para_a_imagem_de_producao()
    {
        var ignorados = Linhas(".dockerignore").Select(l => l.Trim()).ToList();

        Assert.Contains(".env", ignorados);
        Assert.Contains(".env.*", ignorados);
    }

    [Theory]
    [InlineData("POSTGRES_PASSWORD")]
    [InlineData("PGPASSWORD")]
    [InlineData("Admin__SenhaInicial")]
    [InlineData("Integracao__Chave")]
    [InlineData("N8n__WebhookChave")]
    public void Os_segredos_do_compose_de_producao_vem_do_env(string segredo)
    {
        var linhas = Linhas("docker-compose.prod.yml").Where(l => l.TrimStart().StartsWith($"{segredo}:", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(linhas);
        Assert.All(linhas, l => Assert.Matches(@":\s*\$\{", l));
    }

    [Fact]
    public void A_senha_do_banco_na_connection_string_vem_do_env()
    {
        var conexoes = Linhas("docker-compose.prod.yml").Where(l => l.Contains("Password=", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(conexoes);
        Assert.All(conexoes, l => Assert.EndsWith("Password=${POSTGRES_PASSWORD}", l.Trim(), StringComparison.Ordinal));
    }

    [Fact]
    public void A_API_confia_no_IP_que_o_Caddy_tem_na_rede()
    {
        var compose = Ler("docker-compose.prod.yml");

        var proxy = Regex.Match(compose, @"Proxy__Ip:\s*(\S+)").Groups[1].Value;
        var caddy = Regex.Match(compose, @"ipv4_address:\s*(\S+)").Groups[1].Value;

        Assert.NotEmpty(proxy);
        Assert.Equal(caddy, proxy);
    }

    [Theory]
    [InlineData("Strict-Transport-Security \"max-age=31536000\"")]
    [InlineData("X-Content-Type-Options \"nosniff\"")]
    [InlineData("X-Frame-Options \"DENY\"")]
    [InlineData("frame-ancestors 'none'")]
    [InlineData("default-src 'self'")]
    [InlineData("-Server")]
    [InlineData("reverse_proxy api:8080")]
    public void O_Caddy_manda_HSTS_e_os_cabecalhos_de_seguranca(string trecho)
    {
        Assert.Contains(trecho, Ler(Path.Combine("deploy", "Caddyfile")), StringComparison.Ordinal);
    }

    /// <summary>
    /// A pasta do Crm.sln, subindo a partir deste arquivo de código. Vale também com o build fora do repositório, como no
    /// contêiner dos testes (docker-compose.testes.yml).
    /// </summary>
    private static string AcharRaiz([CallerFilePath] string esteArquivo = "")
    {
        for (var pasta = new DirectoryInfo(Path.GetDirectoryName(esteArquivo)!); pasta is not null; pasta = pasta.Parent)
        {
            if (File.Exists(Path.Combine(pasta.FullName, "Crm.sln")))
            {
                return pasta.FullName;
            }
        }

        throw new InvalidOperationException("Não achei a pasta do Crm.sln.");
    }
}
