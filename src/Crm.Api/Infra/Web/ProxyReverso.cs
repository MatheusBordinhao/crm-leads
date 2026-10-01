using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace Crm.Api.Infra.Web;

/// <summary>O proxy reverso na frente da API, na seção <c>Proxy</c> da configuração. Em produção, é o Caddy.</summary>
public sealed class ProxyOptions
{
    public const string Secao = "Proxy";

    /// <summary>
    /// O IP do proxy na rede do Docker. Só dele a API aceita <c>X-Forwarded-For</c> e <c>X-Forwarded-Proto</c>.
    /// Vazio, a API não confia em nenhum proxy e ignora esses cabeçalhos.
    /// </summary>
    public string? Ip { get; set; }
}

internal sealed class ValidacaoProxyOptions : IValidateOptions<ProxyOptions>
{
    public const string Mensagem =
        "Proxy__Ip precisa ser o endereço IP do proxy, como 172.30.0.10 (veja o docker-compose.prod.yml).";

    public ValidateOptionsResult Validate(string? name, ProxyOptions options) =>
        string.IsNullOrWhiteSpace(options.Ip) || IPAddress.TryParse(options.Ip, out _)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(Mensagem);
}

internal static class ProxyReversoExtensions
{
    /// <summary>
    /// Os cabeçalhos encaminhados pelo proxy, aceitos só do IP de <c>Proxy__Ip</c>. Com eles, o limite de logins por IP
    /// e a auditoria veem o IP de quem acessa, e não o do Caddy; um <c>X-Forwarded-For</c> vindo de outro lugar é
    /// ignorado, para ninguém forjar o próprio IP.
    /// </summary>
    public static IServiceCollection AddProxyReverso(this IServiceCollection services)
    {
        services.AddOptions<ProxyOptions>().BindConfiguration(ProxyOptions.Secao).ValidateOnStart();
        services.AddSingleton<IValidateOptions<ProxyOptions>, ValidacaoProxyOptions>();
        services.AddOptions<ForwardedHeadersOptions>().Configure<IOptions<ProxyOptions>>((opcoes, proxy) =>
        {
            // O padrão do ASP.NET Core confia no loopback; aqui, só no proxy configurado. Com as duas listas vazias, o
            // middleware confiaria em qualquer um: sem proxy configurado, nenhum cabeçalho encaminhado vale.
            opcoes.KnownProxies.Clear();
            opcoes.KnownIPNetworks.Clear();
            if (!IPAddress.TryParse(proxy.Value.Ip, out var ip))
            {
                opcoes.ForwardedHeaders = ForwardedHeaders.None;
                return;
            }

            opcoes.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // Uma conexão IPv4 num socket IPv6 chega com o endereço mapeado (::ffff:172.30.0.10).
            opcoes.KnownProxies.Add(ip);
            opcoes.KnownProxies.Add(ip.MapToIPv6());
        });
        return services;
    }
}
