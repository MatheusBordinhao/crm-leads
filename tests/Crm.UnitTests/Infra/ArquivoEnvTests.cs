using Crm.Api.Infra.Configuracao;

namespace Crm.UnitTests.Infra;

public class ArquivoEnvTests
{
    [Fact]
    public void Interpretar_le_nome_e_valor()
    {
        var variaveis = ArquivoEnv.Interpretar("POSTGRES_PASSWORD=segredo");

        Assert.Equal("segredo", variaveis["POSTGRES_PASSWORD"]);
    }

    [Fact]
    public void Interpretar_ignora_comentarios_e_linhas_em_branco()
    {
        var variaveis = ArquivoEnv.Interpretar("# comentário\n\n   \n  # outro\nA=1\n");

        Assert.Equal(new Dictionary<string, string> { ["A"] = "1" }, variaveis);
    }

    [Fact]
    public void Interpretar_remove_comentario_no_fim_da_linha_como_o_compose()
    {
        var variaveis = ArquivoEnv.Interpretar(
            "Crm__UrlPublica=http://localhost:5173        # base dos links nos eventos\nSENHA=abc#123");

        Assert.Equal("http://localhost:5173", variaveis["Crm__UrlPublica"]);
        Assert.Equal("abc#123", variaveis["SENHA"]);
    }

    [Fact]
    public void Interpretar_mantem_cerquilha_no_inicio_do_valor_sem_espaco()
    {
        var variaveis = ArquivoEnv.Interpretar("SENHA=#abc");

        Assert.Equal("#abc", variaveis["SENHA"]);
    }

    [Fact]
    public void Interpretar_mantem_igual_e_ponto_e_virgula_no_valor()
    {
        var variaveis = ArquivoEnv.Interpretar(
            "ConnectionStrings__Crm=Host=localhost;Port=5432;Database=crm;Username=crm;Password=x=y");

        Assert.Equal("Host=localhost;Port=5432;Database=crm;Username=crm;Password=x=y", variaveis["ConnectionStrings__Crm"]);
    }

    [Fact]
    public void Interpretar_remove_aspas_e_mantem_o_conteudo()
    {
        var variaveis = ArquivoEnv.Interpretar("A=\"com espaço # e cerquilha\"\nB='simples'  # comentário");

        Assert.Equal("com espaço # e cerquilha", variaveis["A"]);
        Assert.Equal("simples", variaveis["B"]);
    }

    [Fact]
    public void Interpretar_aceita_bom_crlf_e_export()
    {
        var variaveis = ArquivoEnv.Interpretar("﻿A=1\r\nexport B=2\r\n");

        Assert.Equal("1", variaveis["A"]);
        Assert.Equal("2", variaveis["B"]);
    }

    [Fact]
    public void Interpretar_ignora_valor_vazio_e_linha_sem_igual()
    {
        var variaveis = ArquivoEnv.Interpretar("Admin__Email=\nN8n__WebhookUrl=                 # comentário\nsem_igual\n=sem_nome");

        Assert.Empty(variaveis);
    }

    [Fact]
    public void Carregar_nao_sobrescreve_variavel_que_ja_existe()
    {
        var existente = $"CRM_TESTE_{Guid.NewGuid():N}";
        var nova = $"CRM_TESTE_{Guid.NewGuid():N}";
        var caminho = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.env");
        File.WriteAllText(caminho, $"{existente}=do-arquivo\n{nova}=do-arquivo\n");
        Environment.SetEnvironmentVariable(existente, "do-ambiente");

        try
        {
            var definidas = ArquivoEnv.Carregar(caminho);

            Assert.Equal(1, definidas);
            Assert.Equal("do-ambiente", Environment.GetEnvironmentVariable(existente));
            Assert.Equal("do-arquivo", Environment.GetEnvironmentVariable(nova));
        }
        finally
        {
            Environment.SetEnvironmentVariable(existente, null);
            Environment.SetEnvironmentVariable(nova, null);
            File.Delete(caminho);
        }
    }

    [Fact]
    public void Localizar_sobe_ate_a_pasta_da_solucao()
    {
        var raiz = CriarPastaTemporaria();
        var subpasta = Directory.CreateDirectory(Path.Combine(raiz, "src", "Crm.Api")).FullName;
        File.WriteAllText(Path.Combine(raiz, "Crm.sln"), "");
        File.WriteAllText(Path.Combine(raiz, ".env"), "A=1");

        try
        {
            Assert.Equal(Path.Combine(raiz, ".env"), ArquivoEnv.Localizar(subpasta));
        }
        finally
        {
            Directory.Delete(raiz, recursive: true);
        }
    }

    [Fact]
    public void Localizar_devolve_nulo_quando_a_solucao_nao_tem_env()
    {
        var raiz = CriarPastaTemporaria();
        File.WriteAllText(Path.Combine(raiz, "Crm.sln"), "");

        try
        {
            Assert.Null(ArquivoEnv.Localizar(raiz));
        }
        finally
        {
            Directory.Delete(raiz, recursive: true);
        }
    }

    private static string CriarPastaTemporaria() =>
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"crm-{Guid.NewGuid():N}")).FullName;
}
