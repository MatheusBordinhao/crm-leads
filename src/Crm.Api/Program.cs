using Crm.Api.Infra.Configuracao;
using Crm.Api.Infra.Web;
using Scalar.AspNetCore;

ArquivoEnv.CarregarEmDesenvolvimento();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetailsPtBr();
builder.Services.AddOpenApi(opcoes => opcoes.AddDocumentTransformer((documento, _, _) =>
{
    documento.Info.Title = "CRM de Leads";
    return Task.CompletedTask;
}));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapControllers();

app.Run();
