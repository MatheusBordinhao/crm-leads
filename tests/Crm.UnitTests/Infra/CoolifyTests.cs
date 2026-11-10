using System.Text.RegularExpressions;

namespace Crm.UnitTests.Infra;

/// <summary>
/// A publicação no Coolify (README › Publicação › No Coolify): o docker-compose.coolify.yml, num servidor dividido com
/// outros projetos, e o deploy/Caddyfile.coolify, atrás do Traefik do Coolify. O Compose não publica porta, não fixa nomes,
/// limita cada serviço e tira as senhas das variáveis que o Coolify sorteia. O Caddyfile tem o mesmo log e os mesmos
/// cabeçalhos do Caddyfile de produção e repassa à API só o IP de quem acessa.
/// </summary>
public class CoolifyTests
{
    private const string Compose = "docker-compose.coolify.yml";

    private static readonly string[] Servicos = ["db", "migracoes", "api", "caddy", "backup"];

    [Fact]
    public void O_Compose_tem_os_servicos_da_producao()
    {
        Assert.Equal(Servicos.Order(StringComparer.Ordinal), LerServicos().Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Nenhum_servico_publica_porta()
    {
        Assert.All(LerServicos(), s => Assert.DoesNotContain(s.Value, l => Regex.IsMatch(l, @"^\s+(ports|expose):")));
    }

    [Fact]
    public void Todo_servico_tem_limite_de_memoria_e_de_CPU_peso_menor_e_log_com_rotacao()
    {
        Assert.All(LerServicos(), s =>
        {
            Assert.Matches(@"^\d+m$", Valor(s.Value, "mem_limit"));
            Assert.InRange(double.Parse(Valor(s.Value, "cpus"), System.Globalization.CultureInfo.InvariantCulture), 0.1, 1.0);
            // Metade do peso padrão (1024): numa disputa de CPU, os outros projetos e o Coolify ficam com o dobro.
            Assert.Equal("512", Valor(s.Value, "cpu_shares"));
            Assert.Equal("json-file", Valor(s.Value, "driver"));
            Assert.Equal("10m", Valor(s.Value, "max-size"));
            Assert.Equal("\"5\"", Valor(s.Value, "max-file"));
        });
    }

    [Fact]
    public void Nenhum_nome_fixo_de_conteiner_volume_ou_rede()
    {
        // O Coolify dá os nomes do recurso: um nome fixo poderia colidir com o de outro projeto do servidor.
        Assert.DoesNotContain(Linhas(Compose), l => l.TrimStart().StartsWith("container_name:", StringComparison.Ordinal));
        Assert.DoesNotContain(Secao("volumes"), l => l.TrimStart().StartsWith("name:", StringComparison.Ordinal));
        Assert.DoesNotContain(Secao("networks"), l => l.TrimStart().StartsWith("name:", StringComparison.Ordinal));
    }

    [Fact]
    public void As_migrations_rodam_e_saem_antes_da_API()
    {
        var servicos = LerServicos();

        Assert.Equal("\"no\"", Valor(servicos["migracoes"], "restart"));
        Assert.Equal("true", Valor(servicos["migracoes"], "exclude_from_hc"));
        Assert.Equal("[\"/app/efbundle\"]", Valor(servicos["migracoes"], "entrypoint"));
        Assert.Contains("      migracoes:", servicos["api"]);
        Assert.Equal("service_completed_successfully", Valor(servicos["api"], "condition"));
    }

    [Fact]
    public void A_API_so_confia_no_IP_fixo_do_Caddy_na_rede_interna()
    {
        var servicos = LerServicos();
        var ipDoCaddy = Valor(servicos["caddy"], "ipv4_address");
        var ipDaApi = Valor(servicos["api"], "ipv4_address");

        Assert.Equal(ipDoCaddy, Valor(servicos["api"], "Proxy__Ip"));
        Assert.Equal($"{ipDaApi}:8080", Valor(servicos["caddy"], "API_ENDERECO"));
        Assert.Contains("        - subnet: 10.73.0.0/24", Secao("networks"));
        string[] fixos = [ipDoCaddy, ipDaApi];
        Assert.All(fixos, ip => Assert.StartsWith("10.73.0.", ip, StringComparison.Ordinal));
    }

    [Fact]
    public void As_senhas_vem_das_variaveis_que_o_Coolify_sorteia()
    {
        var servicos = LerServicos();
        var conexoes = Linhas(Compose).Where(l => l.Contains("Password=", StringComparison.Ordinal)).ToList();

        Assert.Equal("${SERVICE_PASSWORD_POSTGRES}", Valor(servicos["db"], "POSTGRES_PASSWORD"));
        Assert.Equal("${SERVICE_PASSWORD_POSTGRES}", Valor(servicos["backup"], "PGPASSWORD"));
        Assert.Equal(2, conexoes.Count);
        Assert.All(conexoes, l => Assert.EndsWith("Password=${SERVICE_PASSWORD_POSTGRES}", l, StringComparison.Ordinal));
        Assert.Equal("${SERVICE_PASSWORD_ADMIN}", Valor(servicos["api"], "Admin__SenhaInicial"));

        // Sem o n8n nesta fase: a integração e os eventos ficam desligados, sem chave nenhuma no Compose.
        string[] desligadas = ["Integracao__Chave", "N8n__WebhookUrl", "N8n__WebhookChave"];
        Assert.All(desligadas, variavel => Assert.Equal("\"\"", Valor(servicos["api"], variavel)));
    }

    [Fact]
    public void A_API_e_as_migrations_usam_a_imagem_do_GHCR_pela_versao_do_commit()
    {
        var servicos = LerServicos();

        string[] daImagemDoCrm = ["api", "migracoes"];
        Assert.All(daImagemDoCrm, s => Assert.Equal("ghcr.io/matheusbordinhao/crm-leads:${CRM_VERSAO:?}", Valor(servicos[s], "image")));
        Assert.DoesNotContain(Linhas(Compose), l => l.TrimStart().StartsWith("build:", StringComparison.Ordinal));
    }

    [Fact]
    public void O_Caddy_monta_o_Caddyfile_do_Coolify_e_o_backup_os_scripts_num_volume()
    {
        var servicos = LerServicos();

        Assert.Contains("      - ./deploy/Caddyfile.coolify:/etc/caddy/Caddyfile:ro", servicos["caddy"]);
        Assert.Contains("      - ./deploy/backup:/backup:ro", servicos["backup"]);
        Assert.Contains("      - backups:/backups", servicos["backup"]);
    }

    [Fact]
    public void O_Caddyfile_do_Coolify_tem_o_mesmo_log_e_os_mesmos_cabecalhos_do_Caddyfile()
    {
        var producao = Ler(Path.Combine("deploy", "Caddyfile"));
        var coolify = Ler(Path.Combine("deploy", "Caddyfile.coolify"));

        Assert.Equal(Bloco(producao, "\tlog {"), Bloco(coolify, "\tlog {"));
        Assert.Equal(Bloco(producao, "\theader {"), Bloco(coolify, "\theader {"));
    }

    [Fact]
    public void O_Caddyfile_do_Coolify_confia_so_no_proxy_da_frente_e_repassa_so_o_IP_de_quem_acessa()
    {
        var linhas = Ler(Path.Combine("deploy", "Caddyfile.coolify")).Split('\n').Select(l => l.Trim()).ToList();

        Assert.Contains("trusted_proxies static private_ranges", linhas);
        Assert.Contains("trusted_proxies_strict", linhas);
        Assert.Contains(":80 {", linhas);
        Assert.Contains("reverse_proxy {$API_ENDERECO} {", linhas);
        Assert.Contains("header_up X-Forwarded-For {client_ip}", linhas);
        Assert.Contains("header_up X-Forwarded-Proto https", linhas);
    }

    private static string Ler(string arquivo) =>
        File.ReadAllText(Path.Combine(Repositorio.Raiz, arquivo)).Replace("\r", "", StringComparison.Ordinal);

    private static List<string> Linhas(string arquivo) => [.. Ler(arquivo).Split('\n')];

    /// <summary>As linhas de cada serviço do Compose, pelo nome: as que vêm entre o nome dele e o próximo.</summary>
    private static Dictionary<string, List<string>> LerServicos()
    {
        var servicos = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        List<string>? atual = null;
        foreach (var linha in Secao("services"))
        {
            var nome = Regex.Match(linha, @"^  ([\w-]+):\s*$");
            if (nome.Success)
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

    /// <summary>As linhas de uma seção do primeiro nível do Compose, como <c>services:</c> ou <c>volumes:</c>.</summary>
    private static List<string> Secao(string chave) =>
    [
        .. Linhas(Compose)
            .SkipWhile(l => l != $"{chave}:")
            .Skip(1)
            .TakeWhile(l => l.Length == 0 || char.IsWhiteSpace(l[0]) || l.StartsWith('#')),
    ];

    /// <summary>O valor de uma chave dentro das linhas de um serviço, a primeira vez que ela aparece.</summary>
    private static string Valor(List<string> servico, string chave)
    {
        var linha = servico.FirstOrDefault(l => Regex.IsMatch(l, $@"^\s+{Regex.Escape(chave)}:\s"))
            ?? throw new InvalidOperationException($"O serviço não tem o {chave}.");
        return linha[(linha.IndexOf(':', StringComparison.Ordinal) + 1)..].Trim();
    }

    /// <summary>O bloco do Caddyfile que começa na linha dada e termina na chave que o fecha, no mesmo recuo.</summary>
    private static string Bloco(string caddyfile, string inicio)
    {
        var linhas = caddyfile.Split('\n');
        var primeira = Array.IndexOf(linhas, inicio);
        Assert.True(primeira >= 0, $"O Caddyfile não tem a linha {inicio.Trim()}.");
        var fim = inicio[..(inicio.Length - inicio.TrimStart().Length)] + "}";
        var ultima = Array.IndexOf(linhas, fim, primeira);
        return string.Join('\n', linhas[primeira..(ultima + 1)]);
    }
}
