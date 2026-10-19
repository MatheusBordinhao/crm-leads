using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Crm.IntegrationTests.Infra;

/// <summary>
/// A pasta do repositório, para os testes que usam arquivos dele (Caddyfile, Compose). Parte do próprio arquivo de código,
/// e não do <c>AppContext.BaseDirectory</c>, que no contêiner dos testes fica fora do repositório.
/// </summary>
internal static class Repositorio
{
    public static string Raiz { get; } = AcharRaiz();

    /// <summary>
    /// A imagem do docker-compose.prod.yml pelo nome, como <c>postgres</c> ou <c>caddy</c>, com a versão fixa: os testes
    /// usam a mesma imagem que o servidor baixa.
    /// </summary>
    public static string ImagemDoComposeDeProducao(string nome)
    {
        var compose = File.ReadAllText(Path.Combine(Raiz, "docker-compose.prod.yml"));
        var imagem = Regex.Match(compose, $@"image:\s*({Regex.Escape(nome)}:\S+)").Groups[1].Value;
        return imagem.Length > 0 ? imagem : throw new InvalidOperationException($"O docker-compose.prod.yml não tem a imagem {nome}.");
    }

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
