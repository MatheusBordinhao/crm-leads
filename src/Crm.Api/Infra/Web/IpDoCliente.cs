using System.Net;

namespace Crm.Api.Infra.Web;

/// <summary>
/// O IP de quem acessa, como a auditoria grava e o limite de logins conta. O Kestrel escuta em <c>[::]</c>, num socket
/// IPv6 que também aceita IPv4, e uma conexão IPv4 chega mapeada (<c>::ffff:172.18.0.1</c>): aqui ela volta a ser IPv4
/// (<c>172.18.0.1</c>), para o mesmo cliente aparecer sempre do mesmo jeito.
/// </summary>
internal static class IpDoCliente
{
    /// <summary>
    /// O IP da conexão, depois do <c>UseForwardedHeaders</c> (atrás do Caddy, o de quem acessa), ou nulo quando a conexão
    /// não tem IP, como no TestServer.
    /// </summary>
    public static string? Texto(HttpContext? contexto) => Normalizar(contexto?.Connection.RemoteIpAddress)?.ToString();

    /// <summary>Um IPv4 mapeado em IPv6 volta a ser IPv4; os outros endereços ficam como estão.</summary>
    public static IPAddress? Normalizar(IPAddress? ip) => ip is { IsIPv4MappedToIPv6: true } ? ip.MapToIPv4() : ip;
}
