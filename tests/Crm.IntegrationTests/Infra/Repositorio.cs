using System.Runtime.CompilerServices;

namespace Crm.IntegrationTests.Infra;

/// <summary>
/// A pasta do repositório, para os testes que usam arquivos dele (Caddyfile, Compose). Parte do próprio arquivo de código,
/// e não do <c>AppContext.BaseDirectory</c>, que no contêiner dos testes fica fora do repositório.
/// </summary>
internal static class Repositorio
{
    public static string Raiz { get; } = AcharRaiz();

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
