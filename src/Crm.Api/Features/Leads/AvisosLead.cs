using Crm.Api.Features.Leads.Normalizacao;

namespace Crm.Api.Features.Leads;

/// <param name="Campo">Campo da ficha a que o aviso se refere, como no JSON: <c>email</c>, <c>telefone</c> ou <c>cpfCnpj</c>.</param>
public sealed record AvisoLead(string Campo, string Mensagem);

/// <summary>
/// Avisos da ficha (seção 3.1), calculados na leitura. A integração e a importação guardam dados em formato
/// inválido em vez de recusar o lead; a ficha mostra o que precisa de atenção.
/// </summary>
public static class AvisosLead
{
    public const string EmailInvalido = "E-mail em formato inválido";

    public const string TelefoneNaoReconhecido = "Telefone não reconhecido";

    public const string DocumentoInvalido = "CPF/CNPJ inválido";

    public const string SemDocumento = "Sem CPF/CNPJ";

    public static IReadOnlyList<AvisoLead> Calcular(string? email, string? telefone, string? cpfCnpj)
    {
        var avisos = new List<AvisoLead>();
        if (email is not null && !Emails.Valido(email))
        {
            avisos.Add(new AvisoLead("email", EmailInvalido));
        }

        if (telefone is not null && !Telefones.Valido(telefone))
        {
            avisos.Add(new AvisoLead("telefone", TelefoneNaoReconhecido));
        }

        if (cpfCnpj is null)
        {
            avisos.Add(new AvisoLead("cpfCnpj", SemDocumento));
        }
        else if (!Documentos.Valido(cpfCnpj))
        {
            avisos.Add(new AvisoLead("cpfCnpj", DocumentoInvalido));
        }

        return avisos;
    }
}
