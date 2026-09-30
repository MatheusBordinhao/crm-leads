using Crm.Api.Features.Leads.Normalizacao;
using FluentValidation;

namespace Crm.Api.Features.Funil;

/// <summary>Cadastro do funil identificado pelo nome: etapa ou motivo de perda.</summary>
public interface ICadastroComNome
{
    string Nome { get; }
}

internal static class RegrasNome
{
    /// <summary>Nome obrigatório e com tamanho máximo, contado depois de tirar os espaços a mais.</summary>
    public static void Aplicar<T>(AbstractValidator<T> validador, int tamanhoMaximo)
        where T : ICadastroComNome =>
        validador.RuleFor(r => r.Nome)
            .Must(n => Textos.Limpar(n) is not null).WithMessage("Informe o nome.")
            .Must(n => Textos.Limpar(n)!.Length <= tamanhoMaximo).WithMessage($"O nome pode ter até {tamanhoMaximo} caracteres.");
}
