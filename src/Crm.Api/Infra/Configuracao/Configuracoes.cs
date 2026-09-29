using Crm.Api.Infra.Dados;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Infra.Configuracao;

/// <summary>Leitura das configurações do sistema (tabela <c>configuracoes</c>), que o admin muda pela tela.</summary>
public sealed class Configuracoes(CrmDbContext db)
{
    /// <summary>Marcar Ganho exige CPF/CNPJ válido (seção 3.4). Sem valor gravado, vale o padrão: ligada.</summary>
    public Task<bool> ExigirDocumentoParaGanhoAsync(CancellationToken ct) =>
        LerBoolAsync(ChavesConfiguracao.ExigirDocumentoParaGanho, padrao: true, ct);

    private async Task<bool> LerBoolAsync(string chave, bool padrao, CancellationToken ct)
    {
        var valor = await db.Configuracoes.Where(c => c.Chave == chave).Select(c => c.Valor).SingleOrDefaultAsync(ct);
        return valor is null ? padrao : bool.Parse(valor);
    }
}

internal static class ConfiguracoesExtensions
{
    public static IServiceCollection AddConfiguracoes(this IServiceCollection services) => services.AddScoped<Configuracoes>();
}
