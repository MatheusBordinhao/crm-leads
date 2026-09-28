using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Infra.Web;

internal static class ApiExtensions
{
    /// <summary>Controllers, JSON (enums como texto, números estritos) e validação com FluentValidation.</summary>
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services
            .AddControllers(opcoes =>
            {
                // Campos obrigatórios são conferidos pelos validadores, com mensagens em português.
                opcoes.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
                opcoes.Filters.Add<ValidacaoFilter>();

                // No OpenAPI, toda resposta de erro é um ProblemDetails.
                opcoes.Filters.Add(new ProducesDefaultResponseTypeAttribute());
            })
            .AddJsonOptions(opcoes => ConfigurarJson(opcoes.JsonSerializerOptions));

        // O documento OpenAPI usa estas opções para descrever os tipos.
        services.ConfigureHttpJsonOptions(opcoes => ConfigurarJson(opcoes.SerializerOptions));

        ValidatorOptions.Global.LanguageManager.Culture = CultureInfo.GetCultureInfo("pt-BR");
        services.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: true);

        return services;
    }

    private static void ConfigurarJson(JsonSerializerOptions opcoes)
    {
        opcoes.Converters.Add(new JsonStringEnumConverter());
        opcoes.NumberHandling = JsonNumberHandling.Strict;

        // Campo ausente não vira o valor padrão (um perfil ou um "ativo" omitido seria perigoso), e o OpenAPI
        // marca os parâmetros dos records como obrigatórios.
        opcoes.RespectRequiredConstructorParameters = true;
    }
}
