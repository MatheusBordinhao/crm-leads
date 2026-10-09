using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.Features.Leads;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.Eventos;
using Crm.IntegrationTests.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests;

/// <summary>
/// Exportação dos dados do titular (seção 3.8) e exclusão de leads de teste (3.2). A anonimização está em
/// <see cref="AnonimizacaoTests"/>.
/// </summary>
public class LgpdTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Dados_do_titular_trazem_tudo_o_que_o_CRM_guarda_do_lead_e_entram_na_auditoria()
    {
        var adminUsuario = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        var atendenteUsuario = await fabrica.CriarUsuarioAsync();
        using var admin = await fabrica.LogarAsync(adminUsuario);
        using var atendente = await fabrica.LogarAsync(atendenteUsuario);
        using var n8n = fabrica.CriarClienteIntegracao();
        var corpo = LeadsDeTeste.CorpoIntegracao();
        corpo["cpfCnpj"] = LeadsDeTeste.CpfUnico();
        var id = await n8n.EnviarLeadIdAsync(corpo);
        (await admin.PostAsJsonAsync($"/api/leads/{id}/responsavel", new { responsavelId = atendenteUsuario.Id }, Ct)).EnsureSuccessStatusCode();
        (await atendente.RegistrarInteracaoAsync(id, "ligacao", "Conversa sobre o orçamento")).EnsureSuccessStatusCode();
        (await atendente.MudarEtapaAsync(id, (await fabrica.EtapaAsync("Perdido")).Id, await fabrica.MotivoPerdaIdAsync("Preço"), "Achou caro"))
            .EnsureSuccessStatusCode();

        var resposta = await admin.GetAsync($"/api/leads/{id}/dados-titular", Ct);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("application/json", resposta.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", resposta.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal($"dados-titular-lead-{id}.json", resposta.Content.Headers.ContentDisposition?.FileName);
        var texto = await resposta.Content.ReadAsStringAsync(Ct);
        Assert.Contains("\n  \"lead\": {", texto, StringComparison.Ordinal);
        var dados = JsonSerializer.Deserialize<JsonElement>(texto);

        var lead = dados.GetProperty("lead");
        Assert.Equal(
            ((string)corpo["nome"]!, (string)corpo["email"]!, (string)corpo["telefone"]!, (string)corpo["cpfCnpj"]!, (string)corpo["empresa"]!),
            (Texto(lead, "nome"), Texto(lead, "email"), Texto(lead, "telefone"), Texto(lead, "cpfCnpj"), Texto(lead, "empresa")));
        Assert.Equal(
            ("meta_lead_ads", "Outubro | Serviço Y", "Perdido", atendenteUsuario.Nome, "Preço", "Achou caro"),
            (Texto(lead, "origem"), Texto(lead, "campanha"), Texto(lead, "etapa"), Texto(lead, "responsavel"), Texto(lead, "motivoPerda"), Texto(lead, "motivoPerdaDetalhe")));
        Assert.Equal("Serviço Y", lead.GetProperty("respostasExtras").GetProperty("Qual serviço você procura?").GetString());

        // A entrada traz o que chegou da integração, como veio.
        var entrada = Assert.Single(dados.GetProperty("entradas").EnumerateArray());
        Assert.Equal(
            ((string)corpo["idExterno"]!, "Outubro | Serviço Y", "criado", "RS | 25-55"),
            (Texto(entrada, "idExterno"), Texto(entrada, "campanha"), Texto(entrada, "situacao"), entrada.GetProperty("conjunto").GetProperty("nome").GetString()));
        Assert.Equal((string)corpo["email"]!, entrada.GetProperty("payloadBruto").GetProperty("email").GetString());

        var interacao = Assert.Single(dados.GetProperty("interacoes").EnumerateArray());
        Assert.Equal(
            ("ligacao", "Conversa sobre o orçamento", atendenteUsuario.Nome),
            (Texto(interacao, "tipo"), Texto(interacao, "descricao"), Texto(interacao, "registradaPor")));
        var mudanca = Assert.Single(dados.GetProperty("historicoEtapas").EnumerateArray());
        Assert.Equal(
            ("Novo", "Perdido", "Preço", "Achou caro", atendenteUsuario.Nome),
            (Texto(mudanca, "de"), Texto(mudanca, "para"), Texto(mudanca, "motivoPerda"), Texto(mudanca, "motivoPerdaDetalhe"), Texto(mudanca, "por")));

        // Os alertas que foram para o n8n, sem o payload, que repetiria os dados do lead.
        var envios = dados.GetProperty("enviosAoN8n").EnumerateArray().ToList();
        Assert.Equal(["lead.criado", "lead.reatribuido"], envios.Select(e => Texto(e, "tipo")));
        Assert.All(envios, e => Assert.Equal(["tipo", "situacao", "criadoEm", "entregueEm"], e.EnumerateObject().Select(p => p.Name)));

        var registro = Assert.Single(
            await fabrica.AuditoriaAsync(EntidadesAuditoria.Lead, id.ToString(CultureInfo.InvariantCulture)), r => r.Acao == AcoesAuditoria.LeadDadosExportados);
        Assert.Equal(adminUsuario.Id, registro.UsuarioId);
        Assert.Null(registro.Detalhes);
    }

    [Fact]
    public async Task Dados_do_titular_sao_so_do_admin_e_lead_que_nao_existe_responde_404()
    {
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var id = await atendente.CriarLeadIdAsync();

        var daAtendente = await atendente.GetAsync($"/api/leads/{id}/dados-titular", Ct);
        var inexistente = await admin.GetAsync($"/api/leads/{long.MaxValue}/dados-titular", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, daAtendente.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        Assert.DoesNotContain(
            await fabrica.AuditoriaAsync(EntidadesAuditoria.Lead, id.ToString(CultureInfo.InvariantCulture)), r => r.Acao == AcoesAuditoria.LeadDadosExportados);
    }

    [Fact]
    public async Task Lead_de_teste_e_excluido_de_vez_com_tudo_o_que_depende_dele()
    {
        var adminUsuario = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        using var admin = await fabrica.LogarAsync(adminUsuario);
        using var n8n = fabrica.CriarClienteIntegracao();
        var corpo = LeadsDeTeste.CorpoIntegracao();
        corpo["teste"] = true;
        var id = await n8n.EnviarLeadIdAsync(corpo);
        var repetida = new Dictionary<string, object?>(corpo) { ["idExterno"] = LeadsDeTeste.IdExternoUnico() };
        Assert.Equal("vinculado", (await (await n8n.EnviarLeadAsync(repetida)).LerJsonAsync()).GetProperty("situacao").GetString());
        (await admin.RegistrarInteracaoAsync(id, "nota", "Lead da ferramenta de teste da Meta")).EnsureSuccessStatusCode();
        (await admin.MudarEtapaAsync(id, (await fabrica.EtapaAsync("Em contato")).Id)).EnsureSuccessStatusCode();

        // Lead de teste não gera notificação nem evento: estes entram direto, para conferir que saem junto.
        await fabrica.NotificarAsync(adminUsuario.Id, id);
        await using (var escopo = fabrica.Services.CreateAsyncScope())
        {
            var banco = escopo.ServiceProvider.GetRequiredService<CrmDbContext>();
            banco.EventosSaida.Add(new EventoSaida
            {
                Id = Guid.CreateVersion7(),
                Tipo = TipoEvento.LeadCriado,
                LeadId = id,
                Payload = JsonDocument.Parse("{}"),
                Situacao = SituacaoEvento.Falhou,
                ProximaTentativaEm = DateTimeOffset.UtcNow,
                CriadoEm = DateTimeOffset.UtcNow,
            });
            await banco.SaveChangesAsync(Ct);
        }

        var resposta = await admin.DeleteAsync($"/api/leads/{id}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/leads/{id}", Ct)).StatusCode);
        await using (var escopo = fabrica.Services.CreateAsyncScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<CrmDbContext>();
            Assert.False(await db.EntradasRecebidas.AnyAsync(e => e.LeadId == id, Ct));
            Assert.False(await db.Interacoes.AnyAsync(i => i.LeadId == id, Ct));
            Assert.False(await db.HistoricoEtapas.AnyAsync(h => h.LeadId == id, Ct));
            Assert.False(await db.Notificacoes.AnyAsync(n => n.LeadId == id, Ct));
            Assert.False(await db.EventosSaida.AnyAsync(e => e.LeadId == id, Ct));
        }

        var registro = Assert.Single(
            await fabrica.AuditoriaAsync(EntidadesAuditoria.Lead, id.ToString(CultureInfo.InvariantCulture)), r => r.Acao == AcoesAuditoria.LeadExcluido);
        Assert.Equal(adminUsuario.Id, registro.UsuarioId);
        Assert.Null(registro.Detalhes);

        // Sem as entradas, o mesmo lead de teste reenviado entra de novo, como um lead novo.
        Assert.Equal(HttpStatusCode.Created, (await n8n.EnviarLeadAsync(corpo)).StatusCode);
    }

    [Fact]
    public async Task Lead_real_nao_e_excluido_e_a_atendente_recebe_403()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var real = await admin.CriarLeadIdAsync();
        var daAtendente = await atendente.CriarLeadIdAsync();

        var recusada = await admin.DeleteAsync($"/api/leads/{real}", Ct);
        var proibida = await atendente.DeleteAsync($"/api/leads/{daAtendente}", Ct);
        var inexistente = await admin.DeleteAsync($"/api/leads/{long.MaxValue}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, recusada.StatusCode);
        Assert.Equal(LeadsController.MensagemSoLeadDeTeste, await recusada.LerDetalheAsync());
        Assert.Equal(HttpStatusCode.Forbidden, proibida.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        Assert.Equal(real, (await fabrica.BuscarLeadAsync(real)).Id);
        Assert.Equal(daAtendente, (await fabrica.BuscarLeadAsync(daAtendente)).Id);
        Assert.DoesNotContain(
            await fabrica.AuditoriaAsync(EntidadesAuditoria.Lead, real.ToString(CultureInfo.InvariantCulture)), r => r.Acao == AcoesAuditoria.LeadExcluido);
    }

    private static string? Texto(JsonElement objeto, string propriedade) => objeto.GetProperty(propriedade).GetString();
}
