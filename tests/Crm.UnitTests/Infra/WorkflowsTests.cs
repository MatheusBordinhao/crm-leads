using System.Text.RegularExpressions;

namespace Crm.UnitTests.Infra;

/// <summary>
/// O CI no GitHub Actions (README › Testes e qualidade › CI): os testes a cada push e PR, a imagem de produção quando muda
/// o que entra nela e as 5 rodadas da suíte do front, disparadas à mão. Os workflows só leem o repositório: não guardam o
/// token do GitHub no .git, não usam segredos nem o pull_request_target, que roda com permissão de escrita, e as ações de
/// terceiros ficam fixadas pelo commit, que não muda como uma tag.
/// </summary>
public class WorkflowsTests
{
    private static readonly string Pasta = Path.Combine(Repositorio.Raiz, ".github", "workflows");

    /// <summary>
    /// O único workflow com permissão de escrita: publica a imagem no GHCR, para o Coolify, só quando disparado à mão e só da
    /// master. Ele tem as regras dele, conferidas abaixo, e segue as outras dos workflows.
    /// </summary>
    private const string PublicarImagem = "publicar-imagem.yml";

    public static TheoryData<string> Workflows() => [.. Directory.GetFiles(Pasta, "*.yml").Select(arquivo => Path.GetFileName(arquivo))];

    public static TheoryData<string> WorkflowsSoDeLeitura() => [.. Workflows().Select(w => w.Data).Where(w => w != PublicarImagem)];

    [Fact]
    public void Os_quatro_workflows_estao_guardados()
    {
        Assert.Superset(
            new HashSet<string> { "testes.yml", "imagem.yml", "front-5-rodadas.yml", PublicarImagem },
            Workflows().Select(w => w.Data).ToHashSet());
    }

    [Theory]
    [MemberData(nameof(WorkflowsSoDeLeitura))]
    public void O_workflow_so_le_o_repositorio(string arquivo)
    {
        var texto = LerWorkflow(arquivo);

        Assert.Equal(["  contents: read"], Bloco(texto, "permissions"));
        Assert.Single(Regex.Matches(texto, @"(?m)^\s*permissions:"));
        Assert.DoesNotMatch(@":\s*write\b", texto);
    }

    [Theory]
    [MemberData(nameof(Workflows))]
    public void As_acoes_de_terceiros_ficam_fixadas_pelo_commit(string arquivo)
    {
        var acoes = Regex.Matches(LerWorkflow(arquivo), @"(?m)^[ ]*(?:- )?uses:\s*(\S+)").Select(m => m.Groups[1].Value).ToList();

        Assert.NotEmpty(acoes);
        Assert.All(acoes, acao => Assert.Matches(@"^[\w.-]+/[\w./-]+@[0-9a-f]{40}$", acao));
    }

    [Theory]
    [MemberData(nameof(Workflows))]
    public void O_checkout_nao_guarda_o_token_no_git(string arquivo)
    {
        // O passo do checkout e as linhas mais recuadas que vêm logo depois dele, que são as dele.
        var checkouts = Regex.Matches(LerWorkflow(arquivo), @"(?m)^([ ]*)- uses: actions/checkout@[^\n]*\n((?:\1  [^\n]*\n)*)");

        Assert.NotEmpty(checkouts);
        Assert.All(checkouts, checkout => Assert.Matches(@"(?m)^[ ]+persist-credentials: false$", checkout.Groups[2].Value));
    }

    [Theory]
    [MemberData(nameof(Workflows))]
    public void O_workflow_nao_usa_o_pull_request_target(string arquivo)
    {
        Assert.DoesNotContain("pull_request_target", LerWorkflow(arquivo));
    }

    [Theory]
    [MemberData(nameof(WorkflowsSoDeLeitura))]
    public void O_workflow_nao_usa_segredos(string arquivo)
    {
        var texto = LerWorkflow(arquivo);

        Assert.DoesNotMatch(@"\bsecrets\s*[.\[:]", texto);
        Assert.DoesNotContain("github.token", texto);
    }

    [Fact]
    public void A_imagem_so_e_publicada_quando_disparada_a_mao_e_da_master()
    {
        var texto = LerWorkflow(PublicarImagem);

        Assert.Equal(["  workflow_dispatch:"], Bloco(texto, "on"));
        Assert.Contains("    if: github.ref == 'refs/heads/master'", texto);
    }

    [Fact]
    public void So_o_job_que_publica_escreve_pacotes()
    {
        var texto = LerWorkflow(PublicarImagem);
        var doJob = Regex.Match(texto, @"(?m)^    permissions:\n((?:      [^\n]+\n)+)");

        Assert.Equal(["  contents: read"], Bloco(texto, "permissions"));
        Assert.Equal(2, Regex.Matches(texto, @"(?m)^\s*permissions:").Count);
        Assert.True(doJob.Success, "O job não tem o permissions dele.");
        Assert.Equal(["      contents: read", "      packages: write"], doJob.Groups[1].Value.TrimEnd('\n').Split('\n'));
        Assert.Single(Regex.Matches(texto, @":\s*write\b"));
    }

    [Fact]
    public void O_token_do_job_entra_so_no_login_pelo_stdin()
    {
        var texto = LerWorkflow(PublicarImagem);

        Assert.Equal(["TOKEN: ${{ secrets.GITHUB_TOKEN }}"], Regex.Matches(texto, @"[^\n]*\bsecrets\b[^\n]*").Select(m => m.Value.Trim()));
        Assert.Contains("printf '%s' \"$TOKEN\" | docker login ghcr.io --username \"$GITHUB_ACTOR\" --password-stdin", texto);
        Assert.DoesNotContain("github.token", texto);
    }

    [Fact]
    public void A_imagem_leva_so_a_tag_do_commit_que_o_Compose_do_Coolify_usa()
    {
        var texto = LerWorkflow(PublicarImagem);
        var compose = Ler("docker-compose.coolify.yml");

        Assert.Contains("      IMAGEM: ghcr.io/matheusbordinhao/crm-leads", texto);
        Assert.Contains("--tag \"${IMAGEM}:${GITHUB_SHA}\" .", texto);
        Assert.Contains("run: docker push \"${IMAGEM}:${GITHUB_SHA}\"", texto);
        Assert.DoesNotContain("latest", texto);
        Assert.Contains("image: ghcr.io/matheusbordinhao/crm-leads:${CRM_VERSAO:?}", compose);
    }

    [Fact]
    public void Os_testes_rodam_a_cada_push_e_PR_com_o_NET_e_o_front_completo()
    {
        var texto = LerWorkflow("testes.yml");

        Assert.Equal(
            ["  push:", "    branches: [master]", "    paths-ignore: ['**.md']", "  pull_request:", "    paths-ignore: ['**.md']"],
            Bloco(texto, "on"));
        Assert.Contains("docker compose -f docker-compose.testes.yml run --rm -T testes", texto);
        Assert.Contains("--entrypoint dotnet testes format Crm.sln --verify-no-changes", texto);
        Assert.Contains("sh .github/scripts/front.sh completo", texto);
    }

    [Fact]
    public void O_conteiner_dos_testes_grava_os_resultados_no_volume_do_build()
    {
        // O código entra só para leitura, e o dotnet test cria a pasta dos resultados na pasta de trabalho. Num checkout
        // limpo, como o do CI, ela não existe, e a suíte falha antes de rodar. Aqui, uma TestResults vazia na cópia do
        // Windows esconde o erro.
        var compose = Ler("docker-compose.testes.yml");

        Assert.Contains("- .:/src:ro", compose);
        Assert.Contains("- artefatos:/artefatos", compose);
        Assert.Matches(@"entrypoint: \[""dotnet"", ""test"",.*""--results-directory"", ""/artefatos/", compose);
    }

    [Fact]
    public void As_5_rodadas_do_front_so_rodam_a_mao_e_em_paralelo()
    {
        var texto = LerWorkflow("front-5-rodadas.yml");

        Assert.Equal(["  workflow_dispatch:"], Bloco(texto, "on"));
        Assert.Contains("fail-fast: false", texto);
        Assert.Contains("rodada: [1, 2, 3, 4, 5]", texto);
        Assert.Contains("sh .github/scripts/front.sh suite", texto);
        Assert.Contains("node .github/scripts/resumo-do-front.mjs", texto);
    }

    [Fact]
    public void A_imagem_e_conferida_quando_muda_o_que_entra_nela()
    {
        var gatilhos = Bloco(LerWorkflow("imagem.yml"), "on");
        string[] arquivos =
        [
            "Dockerfile",
            "docker-compose.yml",
            "docker-compose.prod.yml",
            "docker-compose.testes.yml",
            "Directory.Packages.props",
            "web/package-lock.json",
            "deploy/Caddyfile",
        ];

        Assert.Contains("  push:", gatilhos);
        Assert.Contains("  pull_request:", gatilhos);
        // A mesma lista no push e no PR.
        Assert.All(arquivos, arquivo => Assert.Equal(2, gatilhos.Count(linha => linha.Trim() == $"- {arquivo}")));
    }

    [Fact]
    public void O_front_roda_na_imagem_do_Node_do_Dockerfile()
    {
        var script = Ler(Path.Combine(".github", "scripts", "front.sh"));

        Assert.Contains("FROM \\(node:", script);
        Assert.Contains("\"$raiz/Dockerfile\"", script);
        Assert.DoesNotMatch(@"node:\d", script);
        Assert.All(Workflows(), w => Assert.DoesNotMatch(@"node:\d", LerWorkflow(w.Data)));
    }

    private static string Ler(string caminho) =>
        File.ReadAllText(Path.Combine(Repositorio.Raiz, caminho)).Replace("\r", "", StringComparison.Ordinal);

    private static string LerWorkflow(string arquivo) => Ler(Path.Combine(".github", "workflows", arquivo));

    /// <summary>As linhas do bloco de uma chave do primeiro nível, como o <c>on:</c>, sem as linhas vazias.</summary>
    private static string[] Bloco(string texto, string chave)
    {
        var inicio = Regex.Match(texto, $@"(?m)^{chave}:\n");
        Assert.True(inicio.Success, $"O workflow não tem o {chave}:.");
        return
        [
            .. texto[(inicio.Index + inicio.Length)..]
                .Split('\n')
                .TakeWhile(linha => linha.Length == 0 || linha.StartsWith(' '))
                .Where(linha => linha.Length > 0),
        ];
    }
}
