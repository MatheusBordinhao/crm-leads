using Crm.Api.Features.Leads.Normalizacao;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.Web;
using FluentValidation;

namespace Crm.Api.Features.Leads;

/// <summary>Validação da tela (seção 3.1): estrita, com a mensagem no campo.</summary>
internal static class RegrasLead
{
    public const string MensagemContato = "Informe o e-mail ou o telefone.";

    public const string MensagemEmail = "E-mail inválido.";

    public const string MensagemTelefone = "Telefone inválido. Informe com DDD, como (51) 99999-8888.";

    public const string MensagemDocumento = "CPF ou CNPJ inválido.";

    public const string MensagemUf = "UF inválida.";

    public static readonly string MensagemOrigem =
        $"Origem inválida. Use {string.Join(", ", Origens.DoCadastroManual.Select(EnumTexto<Origem>.Nome))}.";

    /// <summary>
    /// Regras do cadastro e da edição. Os formatos de e-mail, telefone e CPF/CNPJ ficam de fora: o cadastro
    /// confere todos, e a edição, só os campos que mudaram.
    /// </summary>
    public static void RegrasComuns<T>(AbstractValidator<T> validador)
        where T : IDadosLead
    {
        validador.RuleFor(r => r.Nome)
            .NotEmpty().WithMessage("Informe o nome.")
            .Must(n => n.Trim().Length <= Lead.TamanhoMaximoNome)
            .WithMessage($"O nome pode ter até {Lead.TamanhoMaximoNome} caracteres.");
        validador.RuleFor(r => r.Email)
            .Must((r, _) => Emails.Normalizar(r.Email) is not null || Telefones.Normalizar(r.Telefone) is not null)
            .WithMessage(MensagemContato);
        validador.RuleFor(r => r.Uf)
            .Must(uf => string.IsNullOrWhiteSpace(uf) || Ufs.Valida(uf.Trim().ToUpperInvariant()))
            .WithMessage(MensagemUf);
        validador.RuleFor(r => r.Empresa)
            .Must(e => e is null || e.Trim().Length <= Lead.TamanhoMaximoEmpresa)
            .WithMessage($"A empresa pode ter até {Lead.TamanhoMaximoEmpresa} caracteres.");
    }

    public static bool EmailAceito(string? valor) => Emails.Normalizar(valor) is not { } email || Emails.Valido(email);

    public static bool TelefoneAceito(string? valor) => Telefones.Normalizar(valor) is not { } telefone || Telefones.Valido(telefone);

    public static bool DocumentoAceito(string? valor) => Documentos.Normalizar(valor) is not { } documento || Documentos.Valido(documento);
}

internal sealed class CriarLeadRequestValidator : Validador<CriarLeadRequest>
{
    public CriarLeadRequestValidator()
    {
        RegrasLead.RegrasComuns(this);
        RuleFor(r => r.Email).Must(RegrasLead.EmailAceito).WithMessage(RegrasLead.MensagemEmail);
        RuleFor(r => r.Telefone).Must(RegrasLead.TelefoneAceito).WithMessage(RegrasLead.MensagemTelefone);
        RuleFor(r => r.CpfCnpj).Must(RegrasLead.DocumentoAceito).WithMessage(RegrasLead.MensagemDocumento);
        RuleFor(r => r.Origem).Must(o => Origens.DoCadastroManual.Contains(o)).WithMessage(RegrasLead.MensagemOrigem);
    }
}

internal sealed class EditarLeadRequestValidator : Validador<EditarLeadRequest>
{
    public EditarLeadRequestValidator() => RegrasLead.RegrasComuns(this);
}

internal sealed class FiltroLeadsValidator : Validador<FiltroLeads>
{
    public FiltroLeadsValidator()
    {
        RuleFor(f => f.Pagina).GreaterThanOrEqualTo(1).WithMessage("A página começa em 1.");
        RuleFor(f => f.Tamanho)
            .InclusiveBetween(1, FiltroLeads.TamanhoMaximo)
            .WithMessage($"O tamanho da página vai de 1 a {FiltroLeads.TamanhoMaximo}.");
        RuleFor(f => f.Ordem).IsInEnum().WithMessage("Ordem inválida.");
        RuleFor(f => f.Ate)
            .GreaterThanOrEqualTo(f => f.De)
            .When(f => f.De is not null && f.Ate is not null)
            .WithMessage("O fim do período vem depois do início.");
    }
}
