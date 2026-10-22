using System.Text.Json;

namespace Crm.UnitTests.Infra;

/// <summary>
/// Os fluxos do n8n guardados em <c>n8n/</c> (seção 4.5). Os JSONs levam os fluxos do n8n local para o n8n Cloud de produção,
/// e as configurações de execução vão junto: nenhum fluxo do CRM salva execução, nem as que deram certo, nem as com erro, nem as
/// manuais, porque o gatilho guarda o que recebeu, com os dados do lead e, no Alertas e no Google, a chave (seção 3.8). Todos avisam
/// o <c>CRM - Erros</c> quando falham. Os fluxos só usam o que também existe no n8n Cloud: os endereços ficam no nó
/// "Configuração", e os segredos, nas credenciais.
/// </summary>
public class FluxosN8nTests
{
    private const string FluxoDeErros = "crm-erros.json";

    private static readonly string Pasta = Path.Combine(Repositorio.Raiz, "n8n");

    /// <summary>Nós que o n8n Cloud não tem ou que leem o disco e a máquina do n8n.</summary>
    private static readonly string[] NosForaDoCloud =
    [
        "n8n-nodes-base.executeCommand",
        "n8n-nodes-base.readWriteFile",
        "n8n-nodes-base.localFileTrigger",
    ];

    public static TheoryData<string> Fluxos() => [.. Directory.GetFiles(Pasta, "*.json").Select(arquivo => Path.GetFileName(arquivo))];

    [Fact]
    public void Os_fluxos_da_secao_4_5_estao_guardados()
    {
        Assert.Superset(
            new HashSet<string>
            {
                "crm-alertas.json",
                "crm-erros.json",
                "crm-entrada-meta.json",
                "crm-entrada-google.json",
                "crm-entrada-site.json",
                "crm-reenvio-de-pendentes.json",
                "crm-relatorio-semanal-gama.json",
            },
            Fluxos().Select(f => f.Data).ToHashSet());
    }

    [Theory]
    [MemberData(nameof(Fluxos))]
    public void O_fluxo_e_do_CRM_e_nao_salva_nenhuma_execucao(string arquivo)
    {
        using var fluxo = Ler(arquivo);
        var configuracoes = fluxo.RootElement.GetProperty("settings");

        Assert.StartsWith("CRM - ", fluxo.RootElement.GetProperty("name").GetString(), StringComparison.Ordinal);
        Assert.Equal("none", configuracoes.GetProperty("saveDataSuccessExecution").GetString());
        Assert.Equal("none", configuracoes.GetProperty("saveDataErrorExecution").GetString());
        Assert.False(configuracoes.GetProperty("saveManualExecutions").GetBoolean());
        Assert.False(configuracoes.GetProperty("saveExecutionProgress").GetBoolean());
    }

    [Theory]
    [MemberData(nameof(Fluxos))]
    public void O_fluxo_roda_no_horario_de_Brasilia_e_avisa_o_CRM_Erros_quando_falha(string arquivo)
    {
        using var fluxo = Ler(arquivo);
        var configuracoes = fluxo.RootElement.GetProperty("settings");

        Assert.Equal("America/Sao_Paulo", configuracoes.GetProperty("timezone").GetString());
        if (arquivo == FluxoDeErros)
        {
            Assert.False(configuracoes.TryGetProperty("errorWorkflow", out _));
            return;
        }

        using var erros = Ler(FluxoDeErros);
        Assert.Equal(erros.RootElement.GetProperty("id").GetString(), configuracoes.GetProperty("errorWorkflow").GetString());
    }

    [Theory]
    [MemberData(nameof(Fluxos))]
    public void O_fluxo_so_usa_o_que_tambem_existe_no_n8n_Cloud(string arquivo)
    {
        var texto = File.ReadAllText(Path.Combine(Pasta, arquivo));
        using var fluxo = JsonDocument.Parse(texto);
        var tipos = Nos(fluxo).Select(no => no.GetProperty("type").GetString()!).ToList();

        Assert.DoesNotContain("$env", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("$vars", texto, StringComparison.Ordinal);
        Assert.All(tipos, tipo => Assert.StartsWith("n8n-nodes-base.", tipo, StringComparison.Ordinal));
        Assert.Empty(tipos.Intersect(NosForaDoCloud));
        Assert.DoesNotContain("n8n-nodes-base.gmail", tipos);
    }

    [Theory]
    [MemberData(nameof(Fluxos))]
    public void O_fluxo_tem_o_no_de_configuracao_e_nenhum_dado_fixado(string arquivo)
    {
        using var fluxo = Ler(arquivo);

        Assert.Contains(
            Nos(fluxo),
            no => no.GetProperty("name").GetString() == "Configuração" && no.GetProperty("type").GetString() == "n8n-nodes-base.set");
        Assert.True(
            !fluxo.RootElement.TryGetProperty("pinData", out var fixados) || !fixados.EnumerateObject().Any(),
            "O JSON não pode levar dados fixados (pin data): eles têm os dados dos testes.");
    }

    /// <summary>
    /// O e-mail do <c>CRM - Erros</c> leva só o nome do fluxo e o nó (seção 4.5). A mensagem do erro pode trazer um dado pessoal,
    /// como o corpo de uma resposta, e o link da execução não abre nada: nenhuma execução fica salva.
    /// </summary>
    [Fact]
    public void O_email_do_CRM_Erros_nao_leva_a_mensagem_do_erro_nem_o_link()
    {
        var texto = File.ReadAllText(Path.Combine(Pasta, FluxoDeErros));

        Assert.DoesNotContain("execution.error", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("execution?.error", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("trigger.error", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("execution.url", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("execution?.url", texto, StringComparison.Ordinal);
    }

    private static JsonDocument Ler(string arquivo) => JsonDocument.Parse(File.ReadAllText(Path.Combine(Pasta, arquivo)));

    private static IEnumerable<JsonElement> Nos(JsonDocument fluxo) => fluxo.RootElement.GetProperty("nodes").EnumerateArray();
}
