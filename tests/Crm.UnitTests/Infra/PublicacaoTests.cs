using System.Text.RegularExpressions;

namespace Crm.UnitTests.Infra;

/// <summary>Os arquivos da publicação (Passo 5): segredos fora da imagem e do Compose, o proxy certo, o Caddy e as portas.</summary>
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

    /// <summary>Os itens do <c>ports</c> de um serviço, sem as aspas: as linhas com mais recuo que ele, menos os comentários.</summary>
    private static List<string> Portas(List<string> servico)
    {
        var inicio = servico.FindIndex(l => l.Trim() == "ports:");
        if (inicio < 0)
        {
            return [];
        }

        var recuo = Recuo(servico[inicio]);
        var itens = servico.Skip(inicio + 1).TakeWhile(l => l.Trim().Length == 0 || Recuo(l) > recuo).Select(l => l.Trim());

        return [.. itens.Where(l => l.StartsWith("- ", StringComparison.Ordinal)).Select(l => l[2..].Trim().Trim('"'))];
    }

    private static int Recuo(string linha) => linha.Length - linha.TrimStart().Length;

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
    [InlineData("Integracao__ChaveAnterior")]
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

    /// <summary>
    /// O banco, a API e o n8n de desenvolvimento ficam só no próprio computador. Sem o IP, o Docker publica a porta em todas
    /// as interfaces, e a regra que o Docker Desktop cria no firewall do Windows deixa a rede chegar nela. A sintaxe longa
    /// (<c>target:</c>) também derruba o teste.
    /// </summary>
    [Fact]
    public void Toda_porta_do_compose_de_desenvolvimento_fica_no_proprio_computador()
    {
        var servicos = Servicos("docker-compose.yml").Select(s => (Nome: s.Key, Portas: Portas(s.Value))).ToList();

        Assert.Superset(new HashSet<string> { "db", "api", "n8n" }, servicos.Where(s => s.Portas.Count > 0).Select(s => s.Nome).ToHashSet());
        Assert.All(servicos, servico => Assert.All(servico.Portas, porta => Assert.True(
            porta.StartsWith("127.0.0.1:", StringComparison.Ordinal),
            $"A porta {porta} do serviço {servico.Nome} não está no 127.0.0.1, e a rede chegaria nela.")));
    }

    /// <summary>
    /// Em produção, só o Caddy publica portas, todas pelo <c>PORTAS_IP</c>: vazio no servidor, em todas as interfaces, no
    /// IPv4 e no IPv6; <c>127.0.0.1</c> no plano B e nos Compose descartáveis, para a rede não chegar neles.
    /// </summary>
    [Fact]
    public void So_o_Caddy_publica_portas_e_pelo_PORTAS_IP()
    {
        var portas = Servicos("docker-compose.prod.yml").Select(s => (Nome: s.Key, Portas: Portas(s.Value))).Where(s => s.Portas.Count > 0).ToList();

        Assert.Equal(["caddy"], portas.Select(s => s.Nome));
        Assert.All(portas.Single().Portas, porta => Assert.StartsWith("${PORTAS_IP:+${PORTAS_IP}:}", porta, StringComparison.Ordinal));
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
