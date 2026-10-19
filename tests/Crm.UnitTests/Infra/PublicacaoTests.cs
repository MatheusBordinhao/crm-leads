using System.Text.RegularExpressions;

namespace Crm.UnitTests.Infra;

/// <summary>Os arquivos da publicação (Passo 5): segredos fora da imagem e do Compose, o proxy certo e o Caddy.</summary>
public class PublicacaoTests
{
    private static string Ler(string arquivo) => File.ReadAllText(Path.Combine(Repositorio.Raiz, arquivo));

    private static List<string> Linhas(string arquivo) => [.. Ler(arquivo).Split('\n').Select(l => l.TrimEnd('\r'))];

    /// <summary>As linhas de cada serviço do Compose, pelo nome: as que vêm entre o nome dele e o próximo.</summary>
    private static Dictionary<string, List<string>> Servicos(string arquivo)
    {
        var servicos = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var emServicos = false;
        List<string>? atual = null;
        foreach (var linha in Linhas(arquivo))
        {
            // Uma chave sem recuo, como services:, networks: ou volumes:, abre outra seção do arquivo.
            if (linha.Length > 0 && !char.IsWhiteSpace(linha[0]) && !linha.StartsWith('#'))
            {
                emServicos = linha.TrimEnd() == "services:";
                atual = null;
                continue;
            }

            var nome = Regex.Match(linha, @"^  ([\w-]+):\s*$");
            if (emServicos && nome.Success)
            {
                atual = servicos[nome.Groups[1].Value] = [];
            }
            else
            {
                atual?.Add(linha);
            }
        }

        return servicos;
    }

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

    /// <summary>
    /// O log de cada contêiner gira (5 arquivos de 10 MB, o <c>x-logs</c>): sem isso, o log do Docker cresce até encher o
    /// disco do servidor. Vale também para um serviço que entrar depois, como um n8n no mesmo Compose.
    /// </summary>
    [Fact]
    public void Todo_servico_do_compose_de_producao_gira_o_log()
    {
        var servicos = Servicos("docker-compose.prod.yml");

        Assert.Superset(new HashSet<string> { "db", "migracoes", "api", "caddy", "backup" }, servicos.Keys.ToHashSet());
        Assert.All(servicos, servico => Assert.True(
            servico.Value.Any(l => l.Trim() == "logging: *logs"),
            $"O serviço {servico.Key} não tem o logging: *logs, e o log dele não gira."));
    }

    [Theory]
    [InlineData("Strict-Transport-Security \"max-age=31536000\"")]
    [InlineData("X-Content-Type-Options \"nosniff\"")]
    [InlineData("X-Frame-Options \"DENY\"")]
    [InlineData("frame-ancestors 'none'")]
    [InlineData("default-src 'self'")]
    [InlineData("connect-src 'self' wss://{$DOMINIO};")]
    [InlineData("-Server")]
    [InlineData("reverse_proxy api:8080")]
    public void O_Caddy_manda_HSTS_e_os_cabecalhos_de_seguranca(string trecho)
    {
        Assert.Contains(trecho, Ler(Path.Combine("deploy", "Caddyfile")), StringComparison.Ordinal);
    }
}
