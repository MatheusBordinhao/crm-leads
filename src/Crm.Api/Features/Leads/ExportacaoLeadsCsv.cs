using System.Globalization;
using System.Text.Json;
using Crm.Api.Features.Leads.Normalizacao;
using Crm.Api.Infra.Datas;
using Crm.Api.Infra.Web;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Features.Leads;

/// <summary>
/// A lista de leads em CSV para o Excel (<see cref="CsvExcel"/>, seção 3.7), com as colunas da ficha. O telefone e o
/// CPF/CNPJ vão como uma pessoa lê, e o Excel os mostra como texto: sem a máscara, ele tiraria os zeros do começo do CPF e
/// mostraria o CNPJ em notação científica. As datas vão no horário de Brasília.
/// </summary>
internal static class ExportacaoLeadsCsv
{
    public static readonly string[] Cabecalho =
    [
        "Id",
        "Nome",
        "E-mail",
        "Telefone",
        "CPF/CNPJ",
        "UF",
        "Empresa",
        "Etapa",
        "Motivo da perda",
        "Responsável",
        "Origem",
        "Plataforma",
        "Campanha",
        "UTM source",
        "UTM medium",
        "UTM campaign",
        "UTM content",
        "UTM term",
        "GCLID",
        "Respostas do formulário",
        "Entrada",
        "Primeiro contato",
        "Ganho em",
        "Perdido em",
        "Teste",
        "Anonimizado em",
    ];

    /// <summary>O arquivo para a resposta, com o dia da exportação no nome.</summary>
    /// <param name="leads">Os leads, com a etapa, a responsável, a campanha e o motivo de perda carregados.</param>
    public static FileContentResult Arquivo(IEnumerable<Lead> leads, DateOnly hoje) =>
        new(Gerar(leads), CsvExcel.TipoConteudo) { FileDownloadName = $"leads-{hoje.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.csv" };

    public static byte[] Gerar(IEnumerable<Lead> leads)
    {
        var csv = new CsvExcel().Linha(Cabecalho);
        foreach (var lead in leads)
        {
            csv.Linha(
                lead.Id.ToString(CultureInfo.InvariantCulture),
                lead.Nome,
                lead.Email,
                Telefones.ParaLeitura(lead.Telefone),
                Documentos.ParaLeitura(lead.CpfCnpj),
                lead.Uf,
                lead.Empresa,
                lead.Etapa.Nome,
                lead.MotivoPerda?.Nome,
                lead.Responsavel?.Nome,
                Origens.NomeLegivel(lead.Origem),
                lead.Plataforma,
                lead.Campanha?.Nome,
                lead.UtmSource,
                lead.UtmMedium,
                lead.UtmCampaign,
                lead.UtmContent,
                lead.UtmTerm,
                lead.Gclid,
                Respostas(lead.RespostasExtras),
                DataHora(lead.CriadoEm),
                DataHora(lead.PrimeiroContatoEm),
                DataHora(lead.ConvertidoEm),
                DataHora(lead.PerdidoEm),
                lead.Teste ? "Sim" : "Não",
                DataHora(lead.AnonimizadoEm));
        }

        return csv.ParaBytes();
    }

    /// <summary><c>05/10/2026 14:03</c>, no horário de Brasília, que o Excel em português lê como data. Sem data, vazio.</summary>
    public static string? DataHora(DateTimeOffset? instante) =>
        instante is { } valor
            ? TimeZoneInfo.ConvertTime(valor, HorarioBrasilia.Fuso).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
            : null;

    /// <summary>As respostas do formulário numa célula: <c>Pergunta: resposta | Outra pergunta: outra resposta</c>.</summary>
    public static string? Respostas(JsonDocument? respostas) =>
        respostas?.RootElement.ValueKind == JsonValueKind.Object && respostas.RootElement.EnumerateObject().Any()
            ? string.Join(
                " | ",
                respostas.RootElement.EnumerateObject().Select(p =>
                    $"{p.Name}: {(p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.GetRawText())}"))
            : null;
}
