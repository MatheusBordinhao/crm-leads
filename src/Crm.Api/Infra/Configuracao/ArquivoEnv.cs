namespace Crm.Api.Infra.Configuracao;

/// <summary>
/// Lê o <c>.env</c> da raiz do repositório para a API usar a mesma configuração do Docker Compose
/// em desenvolvimento. Só preenche variáveis que ainda não existem no ambiente.
/// </summary>
internal static class ArquivoEnv
{
    private const string ArquivoSolucao = "Crm.sln";

    /// <summary>Carrega o <c>.env</c> quando o ambiente é Development. Nos demais ambientes, não faz nada.</summary>
    public static void CarregarEmDesenvolvimento()
    {
        var ambiente = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        if (!string.Equals(ambiente, Environments.Development, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var caminho = Localizar(Directory.GetCurrentDirectory()) ?? Localizar(AppContext.BaseDirectory);
        if (caminho is not null)
        {
            Carregar(caminho);
        }
    }

    /// <summary>
    /// Sobe a partir de <paramref name="diretorioInicial"/> até achar a pasta do <c>Crm.sln</c> e devolve
    /// o caminho do <c>.env</c> dela, ou <c>null</c> se não houver.
    /// </summary>
    public static string? Localizar(string diretorioInicial)
    {
        for (var pasta = new DirectoryInfo(diretorioInicial); pasta is not null; pasta = pasta.Parent)
        {
            if (File.Exists(Path.Combine(pasta.FullName, ArquivoSolucao)))
            {
                var caminho = Path.Combine(pasta.FullName, ".env");
                return File.Exists(caminho) ? caminho : null;
            }
        }

        return null;
    }

    /// <summary>Define as variáveis do arquivo que ainda não existem no ambiente e devolve quantas definiu.</summary>
    public static int Carregar(string caminho)
    {
        var definidas = 0;
        foreach (var (nome, valor) in Interpretar(File.ReadAllText(caminho)))
        {
            if (Environment.GetEnvironmentVariable(nome) is null)
            {
                Environment.SetEnvironmentVariable(nome, valor);
                definidas++;
            }
        }

        return definidas;
    }

    /// <summary>
    /// Interpreta o conteúdo no mesmo formato que o Docker Compose aceita: <c>NOME=valor</c>, linhas com
    /// <c>#</c> são comentários, <c>#</c> depois de espaço encerra um valor sem aspas e aspas simples ou
    /// duplas delimitam o valor. Valores vazios são ignorados.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Interpretar(string conteudo)
    {
        var variaveis = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var linhaBruta in conteudo.TrimStart('﻿').Split('\n'))
        {
            var linha = linhaBruta.Trim();
            if (linha.Length == 0 || linha.StartsWith('#'))
            {
                continue;
            }

            if (linha.StartsWith("export ", StringComparison.Ordinal))
            {
                linha = linha["export ".Length..].TrimStart();
            }

            var igual = linha.IndexOf('=');
            if (igual <= 0)
            {
                continue;
            }

            var nome = linha[..igual].Trim();
            var valor = LerValor(linha[(igual + 1)..]);
            if (nome.Length > 0 && valor.Length > 0)
            {
                variaveis[nome] = valor;
            }
        }

        return variaveis;
    }

    private static string LerValor(string bruto)
    {
        var valor = bruto.TrimStart();
        if (valor.Length > 0 && (valor[0] == '"' || valor[0] == '\''))
        {
            var fim = valor.IndexOf(valor[0], 1);
            if (fim > 0)
            {
                return valor[1..fim];
            }
        }

        // O comentário começa num # precedido de espaço, inclusive o espaço logo depois do =.
        for (var i = 1; i < bruto.Length; i++)
        {
            if (bruto[i] == '#' && char.IsWhiteSpace(bruto[i - 1]))
            {
                return bruto[..i].Trim();
            }
        }

        return bruto.Trim();
    }
}
