using System.Net;
using System.Net.Http.Json;
using Crm.Api.Features.Funil;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Dados;
using Crm.IntegrationTests.Infra;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests;

/// <summary>Mudança de etapa pela tela (seção 3.4): <c>POST /api/leads/{id}/etapa</c>.</summary>
public class MudancaEtapaTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Dictionary<string, object?> ComCpf(string cpf)
    {
        var corpo = LeadsDeTeste.CorpoLead();
        corpo["cpfCnpj"] = cpf;
        return corpo;
    }

    private async Task<T> GravarAsync<T>(T entidade)
        where T : class
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<CrmDbContext>();
        db.Add(entidade);
        await db.SaveChangesAsync(Ct);
        return entidade;
    }

    [Fact]
    public async Task Atendente_muda_a_etapa_e_o_historico_guarda_quem_e_quando()
    {
        var atendente = await fabrica.CriarUsuarioAsync();
        using var cliente = await fabrica.LogarAsync(atendente);
        var leadId = await cliente.CriarLeadIdAsync();
        var novo = await fabrica.EtapaAsync("Novo");
        var emContato = await fabrica.EtapaAsync("Em contato");
        var antes = DateTimeOffset.UtcNow;

        var resposta = await cliente.MudarEtapaAsync(leadId, emContato.Id);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var ficha = await resposta.LerJsonAsync();
        Assert.Equal("Em contato", ficha.GetProperty("etapa").GetProperty("nome").GetString());
        var mudanca = Assert.Single(await fabrica.HistoricoAsync(leadId));
        Assert.Equal((novo.Id, emContato.Id), (mudanca.EtapaAnteriorId, mudanca.EtapaNovaId));
        Assert.Equal(atendente.Id, mudanca.UsuarioId);
        Assert.InRange(mudanca.OcorridoEm, antes, DateTimeOffset.UtcNow);
        Assert.Null(mudanca.MotivoPerdaId);
    }

    [Fact]
    public async Task Perdido_sem_motivo_responde_400_no_campo_e_nao_muda_nada()
    {
        using var cliente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var leadId = await cliente.CriarLeadIdAsync();
        var perdido = await fabrica.EtapaAsync("Perdido");

        var resposta = await cliente.MudarEtapaAsync(leadId, perdido.Id);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal([RegrasFunil.MensagemMotivoObrigatorio], await resposta.LerErrosDoCampoAsync("motivoPerdaId"));
        Assert.Equal("Novo", (await fabrica.BuscarLeadAsync(leadId)).Etapa.Nome);
        Assert.Empty(await fabrica.HistoricoAsync(leadId));
    }

    [Fact]
    public async Task Perdido_com_motivo_desativado_responde_400()
    {
        using var cliente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var leadId = await cliente.CriarLeadIdAsync();
        var desativado = await GravarAsync(new MotivoPerda { Nome = $"Motivo antigo {Guid.NewGuid():N}", Ativo = false });

        var resposta = await cliente.MudarEtapaAsync(leadId, (await fabrica.EtapaAsync("Perdido")).Id, desativado.Id);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal([RegrasFunil.MensagemMotivoInvalido], await resposta.LerErrosDoCampoAsync("motivoPerdaId"));
    }

    [Fact]
    public async Task Perdido_com_motivo_grava_a_data_o_motivo_e_o_detalhe()
    {
        using var cliente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var leadId = await cliente.CriarLeadIdAsync();
        var motivoId = await fabrica.MotivoPerdaIdAsync("Preço");

        var resposta = await cliente.MudarEtapaAsync(leadId, (await fabrica.EtapaAsync("Perdido")).Id, motivoId, "  Achou caro  ");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var ficha = await resposta.LerJsonAsync();
        Assert.Equal("Preço", ficha.GetProperty("motivoPerda").GetProperty("nome").GetString());
        Assert.Equal("Achou caro", ficha.GetProperty("motivoPerdaDetalhe").GetString());
        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, ficha.GetProperty("perdidoEm").ValueKind);
        var mudanca = Assert.Single(await fabrica.HistoricoAsync(leadId));
        Assert.Equal((motivoId, "Achou caro"), (mudanca.MotivoPerdaId, mudanca.MotivoPerdaDetalhe));
    }

    [Fact]
    public async Task Motivo_numa_etapa_que_nao_e_Perdido_responde_400()
    {
        using var cliente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var leadId = await cliente.CriarLeadIdAsync();

        var resposta = await cliente.MudarEtapaAsync(leadId, (await fabrica.EtapaAsync("Em contato")).Id, await fabrica.MotivoPerdaIdAsync());

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal([RegrasFunil.MensagemMotivoSoEmPerdido], await resposta.LerErrosDoCampoAsync("motivoPerdaId"));
    }

    [Fact]
    public async Task Ganho_sem_documento_valido_e_recusado_com_mensagem_clara()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var semDocumento = await admin.CriarLeadIdAsync();
        using var n8n = fabrica.CriarClienteIntegracao();
        var corpo = LeadsDeTeste.CorpoIntegracao();
        corpo["cpfCnpj"] = "52998224726";
        var comDocumentoInvalido = await n8n.EnviarLeadIdAsync(corpo);
        var ganho = await fabrica.EtapaAsync("Ganho");

        foreach (var leadId in new[] { semDocumento, comDocumentoInvalido })
        {
            var resposta = await admin.MudarEtapaAsync(leadId, ganho.Id);

            Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
            Assert.Equal(RegrasFunil.MensagemDocumentoParaGanho, await resposta.LerDetalheAsync());
            var lead = await fabrica.BuscarLeadAsync(leadId);
            Assert.Equal("Novo", lead.Etapa.Nome);
            Assert.Null(lead.ConvertidoEm);
        }
    }

    [Fact]
    public async Task Ganho_com_documento_valido_grava_a_conversao_e_sair_dele_limpa()
    {
        using var cliente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var leadId = await cliente.CriarLeadIdAsync(ComCpf(LeadsDeTeste.CpfUnico()));

        var ganho = await cliente.MudarEtapaAsync(leadId, (await fabrica.EtapaAsync("Ganho")).Id);
        var convertido = await fabrica.BuscarLeadAsync(leadId);
        var volta = await cliente.MudarEtapaAsync(leadId, (await fabrica.EtapaAsync("Proposta enviada")).Id);

        Assert.Equal(HttpStatusCode.OK, ganho.StatusCode);
        Assert.NotNull(convertido.ConvertidoEm);
        Assert.Equal(HttpStatusCode.OK, volta.StatusCode);
        Assert.Null((await fabrica.BuscarLeadAsync(leadId)).ConvertidoEm);
        Assert.Equal(2, (await fabrica.HistoricoAsync(leadId)).Count);
    }

    [Fact]
    public async Task Ir_para_a_etapa_em_que_o_lead_ja_esta_nao_muda_nada()
    {
        using var cliente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var leadId = await cliente.CriarLeadIdAsync();
        var antes = await fabrica.BuscarLeadAsync(leadId);

        var resposta = await cliente.MudarEtapaAsync(leadId, antes.EtapaId);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Empty(await fabrica.HistoricoAsync(leadId));
        Assert.Equal(antes.Versao, (await fabrica.BuscarLeadAsync(leadId)).Versao);
    }

    [Fact]
    public async Task Etapa_desativada_ou_inexistente_responde_400()
    {
        using var cliente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var leadId = await cliente.CriarLeadIdAsync();
        var desativada = await GravarAsync(new Etapa { Nome = $"Antiga {Guid.NewGuid():N}", Ordem = 90, Tipo = TipoEtapa.Aberta, Ativa = false });

        foreach (var etapaId in new[] { desativada.Id, int.MaxValue })
        {
            var resposta = await cliente.MudarEtapaAsync(leadId, etapaId);

            Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
            Assert.Equal([RegrasFunil.MensagemEtapaInvalida], await resposta.LerErrosDoCampoAsync("etapaId"));
        }

        Assert.Empty(await fabrica.HistoricoAsync(leadId));
    }

    [Fact]
    public async Task Pedido_sem_etapa_ou_com_detalhe_longo_demais_responde_400()
    {
        using var cliente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var leadId = await cliente.CriarLeadIdAsync();
        var perdido = await fabrica.EtapaAsync("Perdido");

        var semEtapa = await cliente.MudarEtapaAsync(leadId, 0);
        var longo = await cliente.MudarEtapaAsync(leadId, perdido.Id, await fabrica.MotivoPerdaIdAsync(), new string('x', 501));
        var semCampos = await cliente.PostAsJsonAsync($"/api/leads/{leadId}/etapa", new { etapaId = perdido.Id }, Ct);

        Assert.Equal(["Escolha a etapa."], await semEtapa.LerErrosDoCampoAsync("etapaId"));
        Assert.Equal(["O detalhe pode ter até 500 caracteres."], await longo.LerErrosDoCampoAsync("motivoPerdaDetalhe"));
        Assert.Equal(HttpStatusCode.BadRequest, semCampos.StatusCode);
        Assert.Empty(await fabrica.HistoricoAsync(leadId));
    }
}
