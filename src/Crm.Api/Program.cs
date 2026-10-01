using Crm.Api.Features.Funil;
using Crm.Api.Features.Integracoes;
using Crm.Api.Features.Leads;
using Crm.Api.Features.Notificacoes;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Configuracao;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.Eventos;
using Crm.Api.Infra.Importacoes;
using Crm.Api.Infra.Web;
using Scalar.AspNetCore;

ArquivoEnv.CarregarEmDesenvolvimento();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApi();
builder.Services.AddProxyReverso();
builder.Services.AddProblemDetailsPtBr();
builder.Services.AddOpenApi(opcoes => opcoes.AddDocumentTransformer((documento, _, _) =>
{
    documento.Info.Title = "CRM de Leads";
    return Task.CompletedTask;
}));
builder.Services.AddCrmDbContext();
builder.Services.AddAcesso();
builder.Services.AddAuditoria();
builder.Services.AddConfiguracoes();
builder.Services.AddFunil();
builder.Services.AddLeads();
builder.Services.AddNotificacoes();
builder.Services.AddEventos();
builder.Services.AddIntegracoes();
builder.Services.AddImportacoes();
builder.Services.AddHealthChecks().AddDbContextCheck<CrmDbContext>("banco");

var app = builder.Build();

app.Configuration.ExigirConnectionString();

// Primeiro: o resto do pipeline já vê o IP e o esquema de quem acessa, e não os do Caddy.
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    await app.AplicarMigrationsAsync();
    app.MapOpenApi();
    app.MapScalarApiReference();
}

await app.InicializarAcessoAsync();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
