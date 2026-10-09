using System.Text.Json;
using Crm.Api.Features.Leads;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.Web;
using FluentValidation;

namespace Crm.Api.Features.Integracoes;

/// <summary>
/// Lead enviado pelo n8n (seção 4.2). Todos os campos são opcionais no JSON: o validador confere os
/// obrigatórios e responde com as mensagens em português. Formato inválido de e-mail, telefone ou documento
/// não recusa o lead.
/// </summary>
/// <param name="Origem"><c>meta_lead_ads</c>, <c>google_ads</c> ou <c>site</c>.</param>
/// <param name="IdExterno">Id do lead na Meta ou no Google (<c>lead_id</c>). No site, um UUID por envio.</param>
/// <param name="RecebidoEm">Quando a origem recebeu o lead, em ISO 8601.</param>
/// <param name="Teste">Lead de teste (ferramenta da Meta, "Enviar dados de teste" do Google). Padrão: <c>false</c>.</param>
/// <param name="Plataforma"><c>facebook</c>, <c>instagram</c>, <c>google</c> ou <c>site</c>.</param>
/// <param name="RespostasExtras">Perguntas extras do formulário e as respostas.</param>
public sealed record EntradaIntegracaoRequest(
    string? Origem = null,
    string? IdExterno = null,
    DateTimeOffset? RecebidoEm = null,
    bool? Teste = null,
    string? Nome = null,
    string? Email = null,
    string? Telefone = null,
    string? CpfCnpj = null,
    string? Uf = null,
    string? Empresa = null,
    string? Plataforma = null,
    ReferenciaExterna? Campanha = null,
    ReferenciaExterna? Conjunto = null,
    ReferenciaExterna? Anuncio = null,
    ReferenciaExterna? Formulario = null,
    Utm? Utm = null,
    string? Gclid = null,
    Dictionary<string, JsonElement>? RespostasExtras = null)
{
    /// <summary>
    /// Sem dados pessoais nem o id externo, que leva até a pessoa na Meta ou no Google (seção 3.8): o MVC imprime os
    /// argumentos da action no log de nível Trace.
    /// </summary>
    public override string ToString() => $"{nameof(EntradaIntegracaoRequest)} {{ Origem = {Origem} }}";
}

/// <param name="Situacao"><c>criado</c> (201), <c>vinculado</c> ou <c>ja_recebido</c> (200).</param>
public sealed record EntradaIntegracaoResponse(long LeadId, ResultadoEntrada Situacao);

/// <summary>Só os problemas de estrutura (seção 4.2): o n8n não reenvia um 400 e avisa o admin.</summary>
internal sealed class EntradaIntegracaoRequestValidator : Validador<EntradaIntegracaoRequest>
{
    public static readonly string MensagemOrigem =
        $"Origem inválida. Use {string.Join(", ", Origens.DaIntegracao.Select(EnumTexto<Origem>.Nome))}.";

    public EntradaIntegracaoRequestValidator()
    {
        RuleFor(r => r.Origem)
            .Must(o => EnumTexto<Origem>.TentarLer(o, out var origem) && Origens.DaIntegracao.Contains(origem))
            .WithMessage(MensagemOrigem);
        RuleFor(r => r.IdExterno)
            .NotEmpty().WithMessage("Informe o idExterno.")
            .Must(id => id!.Trim().Length <= Campanha.TamanhoMaximoIdExterno)
            .WithMessage($"O idExterno pode ter até {Campanha.TamanhoMaximoIdExterno} caracteres.");
        RuleFor(r => r.RecebidoEm).NotNull().WithMessage("Informe o recebidoEm, em ISO 8601.");
        RuleFor(r => r.Nome).NotEmpty().WithMessage("Informe o nome.");
        RuleFor(r => r.Email)
            .Must((r, email) => !string.IsNullOrWhiteSpace(email) || !string.IsNullOrWhiteSpace(r.Telefone))
            .WithMessage("Informe o e-mail ou o telefone.");
    }
}
