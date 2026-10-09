using System.Security.Claims;
using System.Text.Json;
using Crm.Api.Infra.Dados;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crm.Api.Infra.Auditoria;

internal static class AcoesAuditoria
{
    public const string LoginSucesso = "login.sucesso";
    public const string LoginFalha = "login.falha";
    public const string SenhaTrocada = "senha.trocada";
    public const string UsuarioCriado = "usuario.criado";
    public const string UsuarioEditado = "usuario.editado";
    public const string UsuarioSenhaRedefinida = "usuario.senha_redefinida";
    public const string LeadCriado = "lead.criado";
    public const string LeadEditado = "lead.editado";

    /// <summary>Troca de responsável, com <c>detalhes { anteriorId, novoId }</c>. A linha do tempo lê daqui.</summary>
    public const string LeadReatribuido = "lead.reatribuido";

    /// <summary>Exportação dos dados do titular (LGPD), sem detalhes.</summary>
    public const string LeadDadosExportados = "lead.dados_exportados";

    /// <summary>
    /// Anonimização (LGPD), com <c>detalhes { entradas, interacoes, eventos }</c>: quantas entradas e interações foram
    /// limpas e quantos eventos para o n8n foram apagados.
    /// </summary>
    public const string LeadAnonimizado = "lead.anonimizado";

    public const string EtapaCriada = "etapa.criada";
    public const string EtapaEditada = "etapa.editada";
    public const string EtapasReordenadas = "etapas.reordenadas";
    public const string EtapaDesativada = "etapa.desativada";
    public const string EtapaReativada = "etapa.reativada";
    public const string MotivoPerdaCriado = "motivo_perda.criado";
    public const string MotivoPerdaEditado = "motivo_perda.editado";
    public const string ConfiguracaoAlterada = "configuracao.alterada";

    /// <summary>Importação de CSV, com <c>detalhes { origem, linhas }</c>, sem o nome do arquivo.</summary>
    public const string ImportacaoCriada = "importacao.criada";

    /// <summary>
    /// Os leads foram trocados pela massa da demonstração (README › Demonstração), com
    /// <c>detalhes { leads, entradas, leadsApagados }</c>. Sem usuário: foi o comando.
    /// </summary>
    public const string DemonstracaoPreparada = "demonstracao.preparada";
}

internal static class EntidadesAuditoria
{
    public const string Usuario = "usuario";
    public const string Lead = "lead";
    public const string Etapa = "etapa";
    public const string MotivoPerda = "motivo_perda";
    public const string Configuracao = "configuracao";
    public const string Importacao = "importacao";
}

/// <summary>
/// Registra ações na auditoria. O registro entra no contexto e é gravado no próximo <c>SaveChanges</c>,
/// na mesma transação da mudança. Os detalhes não podem ter dados pessoais: use ids e nomes de campos.
/// </summary>
public sealed class AuditoriaService(CrmDbContext db, IHttpContextAccessor http, TimeProvider relogio)
{
    /// <param name="usuarioId">Quem fez a ação. Se omitido, é o usuário logado.</param>
    public void Registrar(
        string acao, string? entidade = null, string? entidadeId = null, object? detalhes = null, Guid? usuarioId = null)
    {
        db.Auditoria.Add(new RegistroAuditoria
        {
            UsuarioId = usuarioId ?? UsuarioLogado(),
            Acao = acao,
            Entidade = entidade,
            EntidadeId = entidadeId,
            Detalhes = detalhes is null ? null : JsonSerializer.SerializeToDocument(detalhes, JsonSerializerOptions.Web),
            Ip = http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            OcorridoEm = relogio.GetUtcNow(),
        });
    }

    private Guid? UsuarioLogado() =>
        Guid.TryParse(http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}

internal static class AuditoriaExtensions
{
    public static IServiceCollection AddAuditoria(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.TryAddSingleton(TimeProvider.System);
        return services.AddScoped<AuditoriaService>();
    }
}
