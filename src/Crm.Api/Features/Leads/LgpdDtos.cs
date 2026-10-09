using System.Text.Json;
using Crm.Api.Infra.Eventos;

namespace Crm.Api.Features.Leads;

/// <summary>
/// Tudo o que o CRM guarda de um lead (LGPD, seção 3.8), para o admin entregar ao titular: o lead, as entradas com o que
/// chegou de cada origem, as interações, o histórico de etapas e os avisos enviados ao n8n, que manda os alertas por
/// e-mail.
/// </summary>
/// <param name="GeradoEm">Quando o arquivo foi gerado.</param>
/// <param name="EnviosAoN8n">Os eventos com o nome e a empresa do lead que foram, ou ainda vão, para o n8n.</param>
public sealed record DadosTitularResponse(
    DateTimeOffset GeradoEm,
    DadosTitularLead Lead,
    IReadOnlyList<DadosTitularEntrada> Entradas,
    IReadOnlyList<DadosTitularInteracao> Interacoes,
    IReadOnlyList<DadosTitularMudancaEtapa> HistoricoEtapas,
    IReadOnlyList<DadosTitularEnvio> EnviosAoN8n);

/// <summary>Os dados do lead, com a etapa, a responsável, a campanha e o motivo de perda pelo nome.</summary>
/// <param name="RespostasExtras">As perguntas do formulário e as respostas, como objeto.</param>
public sealed record DadosTitularLead(
    long Id,
    string Nome,
    string? Email,
    string? Telefone,
    string? CpfCnpj,
    string? Uf,
    string? Empresa,
    Origem Origem,
    string? Plataforma,
    string? Campanha,
    string? UtmSource,
    string? UtmMedium,
    string? UtmCampaign,
    string? UtmContent,
    string? UtmTerm,
    string? Gclid,
    JsonElement? RespostasExtras,
    string Etapa,
    string? Responsavel,
    string? MotivoPerda,
    string? MotivoPerdaDetalhe,
    bool Teste,
    DateTimeOffset CriadoEm,
    DateTimeOffset AtualizadoEm,
    DateTimeOffset? PrimeiroContatoEm,
    DateTimeOffset? ConvertidoEm,
    DateTimeOffset? PerdidoEm,
    DateTimeOffset? AnonimizadoEm);

/// <summary>Uma entrada: cada vez que o lead chegou, pela integração, pelo cadastro ou por uma planilha.</summary>
/// <param name="IdExterno">O id do lead na origem; depois da anonimização, só o hash dele.</param>
/// <param name="Conjunto">O conjunto de anúncios da Meta ou o grupo de anúncios do Google: <c>{ id, nome }</c>.</param>
/// <param name="PayloadBruto">O que chegou, como veio: o corpo da integração ou a linha da planilha.</param>
public sealed record DadosTitularEntrada(
    Origem Origem,
    string? IdExterno,
    string? Campanha,
    JsonElement? Conjunto,
    JsonElement? Anuncio,
    JsonElement? Formulario,
    string? UtmSource,
    string? UtmMedium,
    string? UtmCampaign,
    string? UtmContent,
    string? UtmTerm,
    string? Gclid,
    JsonElement? RespostasExtras,
    JsonElement? PayloadBruto,
    SituacaoEntrada Situacao,
    bool Teste,
    DateTimeOffset RecebidoEm,
    DateTimeOffset ProcessadoEm);

/// <param name="RegistradaPor">O nome de quem registrou.</param>
/// <param name="CriadoEm">Quando foi registrada, que pode ser depois de quando aconteceu.</param>
public sealed record DadosTitularInteracao(TipoInteracao Tipo, string? Descricao, DateTimeOffset OcorridoEm, string RegistradaPor, DateTimeOffset CriadoEm);

/// <param name="Por">O nome de quem mudou. Nulo quando foi o sistema, como na reabertura por uma nova entrada.</param>
public sealed record DadosTitularMudancaEtapa(
    string De, string Para, string? MotivoPerda, string? MotivoPerdaDetalhe, string? Por, DateTimeOffset OcorridoEm);

/// <param name="EntregueEm">Quando o n8n recebeu. Nulo enquanto o evento está pendente ou se ele falhou.</param>
public sealed record DadosTitularEnvio(TipoEvento Tipo, SituacaoEvento Situacao, DateTimeOffset CriadoEm, DateTimeOffset? EntregueEm);
