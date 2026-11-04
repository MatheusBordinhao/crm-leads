using System.Text.RegularExpressions;

namespace Crm.UnitTests.Infra;

/// <summary>
/// O Dependabot avisa quando sai uma versão nova do que tem versão fixa (README › Versões fixadas): um PR por mês para as
/// imagens, com o Dockerfile e os Compose juntos, um para os pacotes do NuGet e um para as ações dos workflows. O npm fica
/// de fora, por causa do Controle Inteligente de Aplicativos do Windows. A versão principal é uma decisão nossa, menos nas
/// ações: nelas, ficar numa versão principal antiga acaba em aviso de descontinuação do GitHub.
/// </summary>
public class DependabotTests
{
    private static readonly string Arquivo = Path.Combine(Repositorio.Raiz, ".github", "dependabot.yml");

    [Fact]
    public void O_Dockerfile_e_os_Compose_saem_num_PR_so_por_mes()
    {
        var texto = File.ReadAllText(Arquivo);
        var entradas = Entradas();

        var grupoDocker = Grupo(entradas["docker"]);
        Assert.NotNull(grupoDocker);
        Assert.Equal(grupoDocker, Grupo(entradas["docker-compose"]));
        Assert.Matches($@"(?m)^  {Regex.Escape(grupoDocker)}:\s*\n    schedule:\s*\n      interval: monthly\s*$", texto.Replace("\r", "", StringComparison.Ordinal));
    }

    [Fact]
    public void O_NuGet_sai_num_PR_por_mes()
    {
        var nuget = Entradas()["nuget"];

        Assert.Contains("interval: monthly", nuget);
        Assert.Contains("groups:", nuget);
    }

    [Fact]
    public void As_acoes_dos_workflows_saem_num_PR_por_mes()
    {
        var acoes = Entradas()["github-actions"];

        Assert.Contains("interval: monthly", acoes);
        Assert.Contains("groups:", acoes);
    }

    [Fact]
    public void O_npm_fica_de_fora()
    {
        Assert.Equal(["docker", "docker-compose", "github-actions", "nuget"], Entradas().Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_versao_principal_fica_de_fora_menos_nas_acoes()
    {
        var entradas = Entradas();
        string[] semAVersaoPrincipal = ["docker", "docker-compose", "nuget"];

        Assert.All(semAVersaoPrincipal, ecossistema => Assert.Contains("version-update:semver-major", entradas[ecossistema]));
        Assert.DoesNotContain("version-update:semver-major", entradas["github-actions"]);
    }

    /// <summary>O texto de cada entrada de <c>updates</c>, pelo ecossistema.</summary>
    private static Dictionary<string, string> Entradas()
    {
        var texto = File.ReadAllText(Arquivo).Replace("\r", "", StringComparison.Ordinal);
        var updates = texto[texto.IndexOf("\nupdates:", StringComparison.Ordinal)..];
        return Regex.Split(updates, @"\n  - (?=package-ecosystem:)")
            .Skip(1)
            .ToDictionary(entrada => Regex.Match(entrada, @"^package-ecosystem: (\S+)").Groups[1].Value, StringComparer.Ordinal);
    }

    private static string? Grupo(string entrada)
    {
        var grupo = Regex.Match(entrada, @"multi-ecosystem-group: (\S+)");
        return grupo.Success ? grupo.Groups[1].Value : null;
    }
}
