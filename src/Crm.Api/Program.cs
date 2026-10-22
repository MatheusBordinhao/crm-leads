using Crm.Api.Features.Funil;
using Crm.Api.Features.Integracoes;
using Crm.Api.Features.Leads;
using Crm.Api.Features.Notificacoes;
using Crm.Api.Features.Painel;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Configuracao;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.Demonstracao;
using Crm.Api.Infra.Eventos;
using Crm.Api.Infra.Importacoes;
using Crm.Api.Infra.TempoReal;
using Crm.Api.Infra.Web;
using Scalar.AspNetCore;

ArquivoEnv.CarregarEmDesenvolvimento();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApi();
builder.Services.AddProxyReverso();
builder.Services.AddChavesDaSessao();
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
builder.Services.AddPainel();
builder.Services.AddEventos();
builder.Services.AddIntegracoes();
builder.Services.AddImportacoes();
builder.Services.AddTempoReal();
builder.Services.AddDemonstracao();
builder.Services.AddHealthChecks().AddDbContextCheck<CrmDbContext>("banco");

var app = builder.Build();

app.Configuration.ExigirConnectionString();

// Primeiro: o resto do pipeline já vê o IP e o esquema de quem acessa, e não os do Caddy.
app.UseForwardedHeaders();

// A BadHttpRequestException, como a do corpo acima do [RequestSizeLimit], responde com o código dela, e não com 500: o n8n
// trata o 413 da integração como recusa da entrada, e um 500 o faria reenviar a mesma entrada para sempre (seção 4.5).
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = erro => erro is BadHttpRequestException requisicaoRuim
        ? requisicaoRuim.StatusCode
        : StatusCodes.Status500InternalServerError,
});
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    await app.AplicarMigrationsAsync();
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// O comando da demonstração (README › Demonstração) troca os leads por uma massa fictícia e termina, sem subir a API.
if (ComandoDemonstracao.Pedido(args))
{
    Environment.ExitCode = await app.PrepararDemonstracaoAsync();
    return;
}

await app.InicializarAcessoAsync();

// O build do React, antes da autenticação: a página e os arquivos dela são públicos.
app.UsarFront();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapControllers();
app.MapTempoReal();

app.Run();
