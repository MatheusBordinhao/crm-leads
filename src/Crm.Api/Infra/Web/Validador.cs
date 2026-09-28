using FluentValidation;

namespace Crm.Api.Infra.Web;

/// <summary>Base dos validadores: para no primeiro erro de cada campo, para mostrar uma mensagem por vez.</summary>
internal abstract class Validador<T> : AbstractValidator<T>
{
    protected Validador() => RuleLevelCascadeMode = CascadeMode.Stop;
}
