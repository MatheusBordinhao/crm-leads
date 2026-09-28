using Crm.IntegrationTests.Infra;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests;

public class AutorizacaoTests(CrmApiFactory fabrica)
{
    private IEnumerable<RouteEndpoint> EndpointsDosControllers() =>
        fabrica.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<ControllerActionDescriptor>() is not null);

    [Fact]
    public void Todo_endpoint_de_controller_declara_a_autorizacao()
    {
        var semDeclaracao = EndpointsDosControllers()
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is null && !e.Metadata.GetOrderedMetadata<IAuthorizeData>().Any())
            .Select(e => e.DisplayName)
            .ToList();

        Assert.NotEmpty(EndpointsDosControllers());
        Assert.Empty(semDeclaracao);
    }

    [Fact]
    public void So_o_login_e_anonimo()
    {
        var anonimos = EndpointsDosControllers()
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(e => $"{e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Single()} /{e.RoutePattern.RawText}");

        Assert.Equal(["POST /api/auth/login"], anonimos);
    }
}
