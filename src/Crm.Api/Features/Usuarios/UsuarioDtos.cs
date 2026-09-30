namespace Crm.Api.Features.Usuarios;

/// <param name="BloqueadoAte">Fim do bloqueio por tentativas erradas, se ainda estiver bloqueado.</param>
/// <param name="TrocarSenha">A senha ainda é provisória.</param>
/// <param name="UltimoLeadAtribuidoEm">A última vez que recebeu um lead pelo rodízio.</param>
/// <param name="ProximoDaVez">Recebe o próximo lead do rodízio, quando a distribuição automática está ligada.</param>
public sealed record UsuarioResponse(
    Guid Id,
    string Nome,
    string Email,
    Perfil Perfil,
    bool Ativo,
    bool ParticipaRodizio,
    bool TrocarSenha,
    DateTimeOffset? BloqueadoAte,
    DateTimeOffset? UltimoLeadAtribuidoEm,
    bool ProximoDaVez,
    DateTimeOffset CriadoEm);

// Os records de requisição não imprimem nome, e-mail nem senha no ToString: o MVC imprime os argumentos da
// action no log de nível Trace.

/// <param name="SenhaProvisoria">O usuário troca no primeiro acesso.</param>
public sealed record CriarUsuarioRequest(string Nome, string Email, Perfil Perfil, bool ParticipaRodizio, string SenhaProvisoria)
{
    public override string ToString() =>
        $"{nameof(CriarUsuarioRequest)} {{ Perfil = {Perfil}, ParticipaRodizio = {ParticipaRodizio} }}";
}

/// <param name="Ativo">Falso desativa: o usuário não entra mais e perde a sessão em até 1 minuto.</param>
public sealed record EditarUsuarioRequest(string Nome, string Email, Perfil Perfil, bool Ativo, bool ParticipaRodizio)
{
    public override string ToString() =>
        $"{nameof(EditarUsuarioRequest)} {{ Perfil = {Perfil}, Ativo = {Ativo}, ParticipaRodizio = {ParticipaRodizio} }}";
}

public sealed record RedefinirSenhaRequest(string SenhaProvisoria)
{
    public override string ToString() => nameof(RedefinirSenhaRequest);
}

/// <summary>Campos que o admin edita, para saber quais mudaram (a auditoria guarda só os nomes).</summary>
internal sealed record DadosEditaveisUsuario(string Nome, string Email, Perfil Perfil, bool Ativo, bool ParticipaRodizio)
{
    public IReadOnlyList<string> CamposDiferentes(DadosEditaveisUsuario outro)
    {
        var campos = new List<string>();
        if (Nome != outro.Nome)
        {
            campos.Add("nome");
        }

        if (Email != outro.Email)
        {
            campos.Add("email");
        }

        if (Perfil != outro.Perfil)
        {
            campos.Add("perfil");
        }

        if (Ativo != outro.Ativo)
        {
            campos.Add("ativo");
        }

        if (ParticipaRodizio != outro.ParticipaRodizio)
        {
            campos.Add("participaRodizio");
        }

        return campos;
    }
}
