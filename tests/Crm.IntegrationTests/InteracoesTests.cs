using System.Net;
using Crm.Api.Features.Leads;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Dados;
using Crm.IntegrationTests.Infra;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests;

/// <summary>Registro de interações e primeiro contato (seção 3.5): <c>POST /api/leads/{id}/interacoes</c>.</summary>
public class InteracoesTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Sem as frações de segundo, para comparar com o que volta do banco (que guarda microssegundos).</summary>
    private static DateTimeOffset EmSegundos(DateTimeOffset instante) =>
        new(instante.Ticks - (instante.Ticks % TimeSpan.TicksPerSecond), TimeSpan.Zero);

    /// <summary>Leva a entrada do lead para ontem: um contato de minutos atrás fica depois dela.</summary>
    private async Task EntrouOntemAsync(long leadId)
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().Leads
            .Where(l => l.Id == leadId)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.CriadoEm, l => l.CriadoEm.AddDays(-1)), Ct);
    }

    private async Task<List<Interacao>> InteracoesDoLeadAsync(long leadId)
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().Interacoes
            .AsNoTracking()
            .Where(i => i.LeadId == leadId)
            .OrderBy(i => i.Id)
            .ToListAsync(Ct);
    }

    [Fact]
    public async Task Primeiro_contato_e_gravado_uma_unica_vez()
    {
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var leadId = await atendente.CriarLeadIdAsync();
        await EntrouOntemAsync(leadId);
        var ligacao = EmSegundos(DateTimeOffset.UtcNow.AddMinutes(-10));

        await atendente.RegistrarInteracaoAsync(leadId, "nota", "Pediu para ligar à tarde");
        var depoisDaNota = (await fabrica.BuscarLeadAsync(leadId)).PrimeiroContatoEm;
        await atendente.RegistrarInteracaoAsync(leadId, "ligacao", "Conversamos", ligacao);
        await atendente.RegistrarInteracaoAsync(leadId, "whatsapp", null, DateTimeOffset.UtcNow);
        await atendente.RegistrarInteracaoAsync(leadId, "reuniao", null, ligacao.AddMinutes(-5));
        await atendente.MudarEtapaAsync(leadId, (await fabrica.EtapaAsync("Em contato")).Id);

        Assert.Null(depoisDaNota);
        Assert.Equal(ligacao, (await fabrica.BuscarLeadAsync(leadId)).PrimeiroContatoEm);
        Assert.Equal(4, (await InteracoesDoLeadAsync(leadId)).Count);
    }

    [Fact]
    public async Task A_primeira_saida_da_etapa_inicial_sem_contato_antes_grava_a_hora_da_mudanca()
    {
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var leadId = await atendente.CriarLeadIdAsync();
        var antes = DateTimeOffset.UtcNow;

        await atendente.MudarEtapaAsync(leadId, (await fabrica.EtapaAsync("Em contato")).Id);
        var depois = DateTimeOffset.UtcNow;
        await atendente.RegistrarInteracaoAsync(leadId, "ligacao");

        var primeiroContato = (await fabrica.BuscarLeadAsync(leadId)).PrimeiroContatoEm;
        Assert.NotNull(primeiroContato);
        Assert.InRange(primeiroContato.Value, antes.AddMilliseconds(-1), depois);
    }

    [Fact]
    public async Task Contato_com_data_anterior_a_entrada_do_lead_vale_a_data_da_entrada()
    {
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var leadId = await atendente.CriarLeadIdAsync();

        await atendente.RegistrarInteracaoAsync(leadId, "ligacao", null, DateTimeOffset.UtcNow.AddDays(-2));

        var lead = await fabrica.BuscarLeadAsync(leadId);
        Assert.Equal(lead.CriadoEm, lead.PrimeiroContatoEm);
    }

    [Fact]
    public async Task Interacoes_simultaneas_gravam_o_primeiro_contato_uma_vez()
    {
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var leadId = await atendente.CriarLeadIdAsync();
        await EntrouOntemAsync(leadId);
        var horarios = Enumerable.Range(1, 5).Select(i => EmSegundos(DateTimeOffset.UtcNow.AddMinutes(-i))).ToList();

        var respostas = await Task.WhenAll(horarios.Select(h => atendente.RegistrarInteracaoAsync(leadId, "ligacao", null, h)));

        Assert.All(respostas, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        Assert.Contains((await fabrica.BuscarLeadAsync(leadId)).PrimeiroContatoEm!.Value, horarios);
        Assert.Equal(5, (await InteracoesDoLeadAsync(leadId)).Count);
    }

    [Fact]
    public async Task Registro_devolve_o_item_da_linha_do_tempo_com_quem_registrou()
    {
        var usuario = await fabrica.CriarUsuarioAsync();
        using var atendente = await fabrica.LogarAsync(usuario);
        var leadId = await atendente.CriarLeadIdAsync();

        var resposta = await atendente.RegistrarInteracaoAsync(leadId, "whatsapp", "  Mandei o orçamento \n  ");
        var semTexto = await atendente.RegistrarInteracaoAsync(leadId, "email", "   ");

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var item = await resposta.LerJsonAsync();
        Assert.Equal("interacao", item.GetProperty("tipo").GetString());
        Assert.Equal(usuario.Nome, item.GetProperty("usuario").GetProperty("nome").GetString());
        Assert.Equal("whatsapp", item.GetProperty("interacao").GetProperty("tipo").GetString());
        Assert.Equal("Mandei o orçamento", item.GetProperty("interacao").GetProperty("descricao").GetString());
        Assert.Equal(HttpStatusCode.Created, semTexto.StatusCode);
        var interacoes = await InteracoesDoLeadAsync(leadId);
        Assert.Equal([usuario.Id, usuario.Id], interacoes.Select(i => i.UsuarioId));
        Assert.Null(interacoes[1].Descricao);
    }

    [Fact]
    public async Task Nota_sem_texto_e_data_no_futuro_sao_recusadas()
    {
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var leadId = await atendente.CriarLeadIdAsync();

        var notaVazia = await atendente.RegistrarInteracaoAsync(leadId, "nota", "  ");
        var futura = await atendente.RegistrarInteracaoAsync(leadId, "ligacao", null, DateTimeOffset.UtcNow.AddHours(1));
        var longa = await atendente.RegistrarInteracaoAsync(leadId, "nota", new string('x', 2001));
        var tipoInvalido = await atendente.RegistrarInteracaoAsync(leadId, "fax");

        Assert.Equal(["Escreva a nota."], await notaVazia.LerErrosDoCampoAsync("descricao"));
        Assert.Equal(["A data não pode estar no futuro."], await futura.LerErrosDoCampoAsync("ocorridoEm"));
        Assert.Equal(["A descrição pode ter até 2000 caracteres."], await longa.LerErrosDoCampoAsync("descricao"));
        Assert.Equal(HttpStatusCode.BadRequest, tipoInvalido.StatusCode);
        Assert.Empty(await InteracoesDoLeadAsync(leadId));
        Assert.Null((await fabrica.BuscarLeadAsync(leadId)).PrimeiroContatoEm);
    }

    [Fact]
    public async Task Relogio_do_navegador_um_pouco_adiantado_vale_como_agora()
    {
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var leadId = await atendente.CriarLeadIdAsync();

        var resposta = await atendente.RegistrarInteracaoAsync(leadId, "ligacao", null, DateTimeOffset.UtcNow.AddMinutes(2));
        var depois = DateTimeOffset.UtcNow;

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.True(Assert.Single(await InteracoesDoLeadAsync(leadId)).OcorridoEm <= depois);
        Assert.True((await fabrica.BuscarLeadAsync(leadId)).PrimeiroContatoEm <= depois);
    }

    [Fact]
    public void Nao_ha_rota_para_editar_nem_excluir_interacoes()
    {
        var metodos = fabrica.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<ControllerActionDescriptor>() is not null
                && e.RoutePattern.RawText!.Contains("interacoes", StringComparison.Ordinal))
            .SelectMany(e => e.Metadata.GetRequiredMetadata<HttpMethodMetadata>().HttpMethods)
            .ToList();

        Assert.Equal(["POST"], metodos);
    }
}
