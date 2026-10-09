using System.Security.Cryptography;
using System.Text;

namespace Crm.Api.Features.Leads;

/// <summary>
/// O id externo de uma entrada anonimizada (seção 3.8): <c>anon:</c> e o SHA-256 do id, em hexadecimal. O banco deixa de
/// guardar o id do lead na Meta ou no Google, que levaria até a pessoa lá, e um reenvio do mesmo id continua sendo
/// reconhecido como já recebido. Cabe nos 100 caracteres do id externo.
/// </summary>
public static class IdExternoAnonimo
{
    public const string Prefixo = "anon:";

    public static string De(string idExterno) =>
        Prefixo + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(idExterno)));
}
