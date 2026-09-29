namespace Crm.Api.Features.Usuarios;

/// <summary>Perfis de acesso. Cada um é um role do Identity com o mesmo nome.</summary>
/// <remarks>
/// Os valores começam em 1: o zero, valor padrão de qualquer enum, não é um perfil válido. Assim, um perfil
/// esquecido no código ou enviado como 0 é recusado pela validação em vez de virar Admin.
/// </remarks>
public enum Perfil
{
    Admin = 1,
    Atendente = 2,
}
