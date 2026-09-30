using System.Text.Json;
using System.Text.Json.Serialization;
using Crm.Api.Features.Funil;
using Crm.Api.Features.Leads;

namespace Crm.Api.Infra.Eventos;

/// <summary>
/// O JSON de um evento para o n8n (seção 4.3). Não leva e-mail, telefone nem CPF/CNPJ do lead: quem precisa
/// deles abre a ficha pelo link.
/// </summary>
/// <param name="Responsavel">Nula quando o lead está sem responsável ou a responsável está desativada.</param>
/// <param name="Dados">Muda conforme o tipo: veja <see cref="DadosEntradaRepetida"/> e os outros.</param>
internal sealed record EventoN8n(
    Guid Id, TipoEvento Tipo, DateTimeOffset OcorridoEm, LeadNoEvento Lead, ResponsavelNoEvento? Responsavel, object Dados)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerOptions.Web) { Converters = { new JsonStringEnumConverter() } };
}

/// <param name="Campanha">O nome da campanha da primeira entrada.</param>
/// <param name="IdExterno">O id da primeira entrada na origem, como o id do lead na Meta.</param>
/// <param name="Url">O link da ficha no CRM.</param>
internal sealed record LeadNoEvento(
    long Id,
    string Nome,
    string? Empresa,
    string? Uf,
    Origem Origem,
    string? Plataforma,
    string? Campanha,
    string? IdExterno,
    EtapaNoEvento Etapa,
    string Url);

internal sealed record EtapaNoEvento(int Id, string Nome, TipoEtapa Tipo);

/// <param name="Email">Para onde o n8n manda o e-mail de alerta.</param>
internal sealed record ResponsavelNoEvento(Guid Id, string Nome, string Email);

internal sealed record PessoaNoEvento(Guid Id, string Nome);

/// <summary><c>dados</c> do <c>lead.entrada_repetida</c>: a origem e a campanha da nova entrada.</summary>
internal sealed record DadosEntradaRepetida(Origem Origem, string? Campanha);

/// <summary><c>dados</c> do <c>lead.reatribuido</c>. Sem responsável antes, a anterior vai nula.</summary>
internal sealed record DadosReatribuido(PessoaNoEvento? ResponsavelAnterior);

/// <summary><c>dados</c> do <c>lead.etapa_alterada</c>. O motivo só vem quando o lead foi para Perdido.</summary>
internal sealed record DadosEtapaAlterada(EtapaNoEvento EtapaAnterior, string? MotivoPerda);
