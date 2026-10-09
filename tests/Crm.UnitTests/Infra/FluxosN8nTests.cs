using System.Text.Json;

namespace Crm.UnitTests.Infra;

/// <summary>
/// Os fluxos do n8n guardados em <c>n8n/</c> (seção 4.5). Os JSONs levam os fluxos para o n8n de produção, e as configurações
/// de execução vão junto: nenhum fluxo do CRM salva as execuções de produção que deram certo, para o n8n não guardar dados
/// pessoais sem necessidade (seção 3.8). Vale também para os fluxos que ainda vão ser guardados, como o do relatório semanal.
/// </summary>
public class FluxosN8nTests
{
    private static readonly string Pasta = Path.Combine(Repositorio.Raiz, "n8n");

    public static TheoryData<string> Fluxos() => [.. Directory.GetFiles(Pasta, "*.json").Select(arquivo => Path.GetFileName(arquivo))];

    [Fact]
    public void Os_fluxos_do_Passo_4_estao_guardados()
    {
        Assert.Superset(new HashSet<string> { "crm-alertas.json", "crm-erros.json" }, Fluxos().Select(f => f.Data).ToHashSet());
    }

    [Theory]
    [MemberData(nameof(Fluxos))]
    public void O_fluxo_e_do_CRM_e_nao_salva_as_execucoes_de_producao_que_deram_certo(string arquivo)
    {
        using var fluxo = JsonDocument.Parse(File.ReadAllText(Path.Combine(Pasta, arquivo)));

        Assert.StartsWith("CRM - ", fluxo.RootElement.GetProperty("name").GetString(), StringComparison.Ordinal);
        Assert.Equal("none", fluxo.RootElement.GetProperty("settings").GetProperty("saveDataSuccessExecution").GetString());
    }
}
