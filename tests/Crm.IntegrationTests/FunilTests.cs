using System.Net;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Configuracao;
using Crm.Api.Infra.Dados;
using Crm.IntegrationTests.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Crm.IntegrationTests;

public class FunilTests(CrmApiFactory fabrica)
{
    private static readonly string[] EtapasIniciais = ["Novo", "Em contato", "Qualificado", "Proposta enviada", "Ganho", "Perdido"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Atendente_ve_as_etapas_iniciais_na_ordem_com_o_tipo()
    {
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);

        var resposta = await atendente.GetAsync("/api/etapas", Ct);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var etapas = (await resposta.LerJsonAsync()).EnumerateArray()
            .Where(e => EtapasIniciais.Contains(e.GetProperty("nome").GetString()))
            .Select(e => (Nome: e.GetProperty("nome").GetString(), Tipo: e.GetProperty("tipo").GetString()))
            .ToList();
        Assert.Equal(EtapasIniciais, etapas.Select(e => e.Nome));
        Assert.Equal(["aberta", "aberta", "aberta", "aberta", "ganho", "perdido"], etapas.Select(e => e.Tipo));
    }

    [Fact]
    public async Task Motivos_de_perda_e_configuracoes_vem_com_os_valores_iniciais()
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<CrmDbContext>();

        var motivos = await db.MotivosPerda.Select(m => m.Nome).ToListAsync(Ct);
        var configuracoes = await db.Configuracoes.ToDictionaryAsync(c => c.Chave, c => c.Valor, Ct);

        Assert.Subset(
            new HashSet<string> { "Sem resposta", "Sem interesse", "Preço", "Fechou com concorrente", "Fora do perfil", "Contato inválido" },
            motivos.ToHashSet());
        Assert.Equal("true", configuracoes[ChavesConfiguracao.DistribuicaoAutomatica]);
        Assert.Equal("true", configuracoes[ChavesConfiguracao.ExigirDocumentoParaGanho]);
    }

    [Fact]
    public async Task Nao_pode_haver_duas_etapas_ativas_de_ganho()
    {
        await using var conexao = new NpgsqlConnection(fabrica.ConnectionString);
        await conexao.OpenAsync(Ct);
        await using var transacao = await conexao.BeginTransactionAsync(Ct);
        await using var comando = new NpgsqlCommand(
            "INSERT INTO etapas (nome, ordem, tipo, ativa) VALUES ('Outro ganho', 99, 'ganho', true)", conexao, transacao);

        var erro = await Assert.ThrowsAsync<PostgresException>(() => comando.ExecuteNonQueryAsync(Ct));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, erro.SqlState);
    }
}
