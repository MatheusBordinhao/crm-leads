using System.Text.RegularExpressions;

namespace Crm.UnitTests.Infra;

/// <summary>
/// As imagens do Docker têm versão fixa (README › Versões fixadas). O SDK do .NET é o mesmo no build de produção, na API de
/// desenvolvimento, no dotnet ef e nos testes, e a imagem de produção roda um ASP.NET Core com a correção da CVE-2026-70159.
/// </summary>
public class ImagensTests
{
    private const string Sdk = "mcr.microsoft.com/dotnet/sdk";

    private const string Runtime = "mcr.microsoft.com/dotnet/aspnet";

    /// <summary>A primeira versão do ASP.NET Core 10 com a correção da CVE-2026-70159, no SignalR.</summary>
    private static readonly Version RuntimeCorrigido = new(10, 0, 13);

    private static readonly string[] Arquivos = ["Dockerfile", "docker-compose.yml", "docker-compose.prod.yml", "docker-compose.testes.yml"];

    [Fact]
    public void O_SDK_do_NET_e_o_mesmo_no_build_de_producao_no_desenvolvimento_e_nos_testes()
    {
        var sdks = Imagens().Where(i => i.Nome == Sdk).ToList();

        Assert.Equal(["Dockerfile", "docker-compose.testes.yml", "docker-compose.yml"], sdks.Select(i => i.Arquivo).Distinct().Order(StringComparer.Ordinal));
        Assert.Single(sdks.Select(i => i.Tag).Distinct());
    }

    [Fact]
    public void As_imagens_do_NET_tem_a_versao_completa()
    {
        var dotnet = Imagens().Where(i => i.Nome is Sdk or Runtime).ToList();

        Assert.NotEmpty(dotnet);
        Assert.All(dotnet, i => Assert.Matches(@"^\d+\.\d+\.\d+$", i.Tag));
    }

    [Fact]
    public void A_imagem_de_producao_tem_o_ASP_NET_Core_com_a_correcao_da_CVE_2026_70159()
    {
        var runtime = Assert.Single(Imagens(), i => i.Nome == Runtime);

        Assert.Equal("Dockerfile", runtime.Arquivo);
        Assert.True(Version.Parse(runtime.Tag) >= RuntimeCorrigido, $"{runtime.Nome}:{runtime.Tag} é anterior à {RuntimeCorrigido}.");
    }

    /// <summary>Cada imagem de um FROM do Dockerfile ou de um image: dos Compose.</summary>
    private static List<Imagem> Imagens() =>
    [
        .. Arquivos.SelectMany(arquivo => Regex
            .Matches(File.ReadAllText(Path.Combine(Repositorio.Raiz, arquivo)), @"^\s*(?:FROM|image:)\s+(\S+)", RegexOptions.Multiline)
            .Select(m => Imagem.Ler(arquivo, m.Groups[1].Value))),
    ];

    private sealed record Imagem(string Arquivo, string Nome, string Tag)
    {
        public static Imagem Ler(string arquivo, string referencia)
        {
            var separador = referencia.LastIndexOf(':');
            return separador < 0 ? new(arquivo, referencia, "") : new(arquivo, referencia[..separador], referencia[(separador + 1)..]);
        }
    }
}
