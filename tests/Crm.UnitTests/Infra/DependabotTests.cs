using System.Text.RegularExpressions;

namespace Crm.UnitTests.Infra;

/// <summary>
/// O Dependabot avisa quando sai uma versão nova do que tem versão fixa (README › Versões fixadas): um PR por mês para as
/// imagens, com o Dockerfile e os Compose juntos, e um para os pacotes do NuGet. O npm fica de fora, por causa do
/// Controle Inteligente de Aplicativos do Windows, e a versão principal é uma decisão nossa.
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
    public void O_npm_e_as_versoes_principais_ficam_de_fora()
    {
        var entradas = Entradas();

        Assert.Equal(["docker", "docker-compose", "nuget"], entradas.Keys.Order(StringComparer.Ordinal));
        Assert.All(entradas.Values, entrada => Assert.Contains("version-update:semver-major", entrada));
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
