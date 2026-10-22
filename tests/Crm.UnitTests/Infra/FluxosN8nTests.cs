using System.Text.Json;
using System.Text.RegularExpressions;

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

    /// <summary>Os fluxos que mandam entradas ao CRM e classificam a falha do envio (seção 4.5).</summary>
    public static TheoryData<string> FluxosQueEnviamAoCrm() =>
    [
        "crm-entrada-site.json",
        "crm-entrada-google.json",
        "crm-entrada-meta.json",
    ];

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

    /// <summary>
    /// A recusa da própria entrada (400 e 413) vai para as recusadas, e a falha de configuração (401, 403, 404 e qualquer outro
    /// código) vai para a fila, com o e-mail na hora (seção 4.5). A regra fica numa função só, com o mesmo texto em todos os
    /// fluxos que enviam ao CRM: mudar um deles sem os outros derruba o teste.
    /// </summary>
    [Fact]
    public void A_regra_do_destino_da_falha_e_a_mesma_nos_fluxos_que_enviam_ao_CRM()
    {
        var regras = FluxosQueEnviamAoCrm().Select(f => f.Data).Select(arquivo =>
        {
            using var fluxo = Ler(arquivo);
            var codigo = No(fluxo, "Classificar a falha").GetProperty("parameters").GetProperty("jsCode").GetString()!;
            var regra = Regex.Match(codigo, @"function destinoDaFalha\(codigo\) \{.*?\n\}", RegexOptions.Singleline);
            Assert.True(regra.Success, $"O Classificar a falha de {arquivo} não tem a função destinoDaFalha.");
            return regra.Value;
        }).ToList();

        Assert.Single(regras.Distinct());
        Assert.Contains("if (codigo === '400' || codigo === '413') return 'recusada';", regras[0], StringComparison.Ordinal);
        Assert.Contains("codigo === '' || codigo === '408' || codigo === '429'", regras[0], StringComparison.Ordinal);
        Assert.EndsWith("return 'configuracao';\n}", regras[0], StringComparison.Ordinal);
    }

    /// <summary>
    /// Um Stop and Error dispara o <c>CRM - Erros</c>, com um e-mail a cada vez: um robô mandando envios inválidos lotaria a caixa
    /// do admin. As recusas ficam nas Data Tables, e a falha de configuração manda o próprio e-mail (seção 4.5).
    /// </summary>
    [Theory]
    [MemberData(nameof(FluxosQueEnviamAoCrm))]
    public void O_fluxo_que_envia_ao_CRM_nao_termina_com_erro_de_proposito(string arquivo)
    {
        using var fluxo = Ler(arquivo);

        Assert.DoesNotContain(Nos(fluxo), no => no.GetProperty("type").GetString() == "n8n-nodes-base.stopAndError");
    }

    /// <summary>
    /// Todo HTTP Request que chama o CRM usa a credencial de integração. Uma credencial de teste esquecida, como a de um teste do
    /// 401, derruba o teste antes de o fluxo chegar ao n8n de produção.
    /// </summary>
    [Theory]
    [MemberData(nameof(Fluxos))]
    public void Todo_HTTP_Request_ao_CRM_usa_a_credencial_de_integracao(string arquivo)
    {
        using var fluxo = Ler(arquivo);
        var chamadasAoCrm = Nos(fluxo).Where(no =>
            no.GetProperty("type").GetString() == "n8n-nodes-base.httpRequest"
            && no.GetProperty("parameters").GetProperty("url").GetString()!.Contains("urlCrm", StringComparison.Ordinal));

        Assert.All(chamadasAoCrm, no => Assert.Equal(
            "CRM - Chave de integração",
            no.GetProperty("credentials").GetProperty("httpHeaderAuth").GetProperty("name").GetString()));
    }

    /// <summary>
    /// A Data Table vai pelo nome, que a importação no n8n Cloud mantém, e não pelo id. Nenhum nó de Data Table lê os cabeçalhos
    /// nem o corpo bruto do gatilho: eles trazem o <c>X-Crm-Chave</c> e o <c>google_key</c>, e as linhas ficam dias nas tabelas.
    /// </summary>
    [Theory]
    [MemberData(nameof(Fluxos))]
    public void Os_nos_de_Data_Table_vao_pelo_nome_e_nao_guardam_o_que_o_gatilho_recebeu(string arquivo)
    {
        using var fluxo = Ler(arquivo);

        Assert.All(
            Nos(fluxo).Where(no => no.GetProperty("type").GetString() == "n8n-nodes-base.dataTable"),
            no =>
            {
                var parametros = no.GetProperty("parameters");
                Assert.Equal("name", parametros.GetProperty("dataTableId").GetProperty("mode").GetString());
                var texto = parametros.GetRawText();
                Assert.DoesNotContain("headers", texto, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("google_key", texto, StringComparison.Ordinal);
                Assert.DoesNotContain(".body", texto, StringComparison.Ordinal);
            });
    }

    private static JsonDocument Ler(string arquivo) => JsonDocument.Parse(File.ReadAllText(Path.Combine(Pasta, arquivo)));

    private static JsonElement No(JsonDocument fluxo, string nome) => Nos(fluxo).Single(no => no.GetProperty("name").GetString() == nome);

    private static IEnumerable<JsonElement> Nos(JsonDocument fluxo) => fluxo.RootElement.GetProperty("nodes").EnumerateArray();
}
