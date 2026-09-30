using Crm.Api.Features.Leads;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Configuracao;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.Eventos;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Features.Notificacoes;

/// <summary>
/// Quem é avisado de cada acontecimento de um lead, num lugar só: as notificações no sistema (seção 3.6) e o evento
/// para o n8n, que manda o e-mail (seção 4.3). Tudo entra no contexto e é gravado na mesma transação do
/// acontecimento. Lead de teste não gera aviso, e o autor da ação nunca é avisado dela: quando quem fez é a própria
/// responsável, nem o evento é gravado. Uma responsável desativada conta como sem responsável: o aviso vai para os
/// admins.
/// </summary>
public sealed class AvisosLeads(CrmDbContext db, Notificador notificador, EventosSaida eventos, Configuracoes configuracoes)
{
    private static readonly object SemDados = new { };

    /// <summary>Lead novo: avisa a responsável ou, se ele ficou sem responsável, os admins.</summary>
    /// <param name="autorId">Quem cadastrou o lead. Nulo quando ele veio da integração.</param>
    public async Task LeadCriadoAsync(Lead lead, Guid? autorId, CancellationToken ct)
    {
        if (lead.Teste)
        {
            return;
        }

        await RegistrarEventoAsync(TipoEvento.LeadCriado, lead, SemDados, autorId, ct);
        var autor = await AutorAsync(autorId, ct);
        if (await ResponsavelAsync(lead, ct) is { Ativo: true } responsavel)
        {
            var titulo = autor is null ? "Novo lead para você" : $"{autor.Nome} cadastrou um lead para você";
            notificador.ParaUsuario(responsavel.Id, TipoNotificacao.LeadAtribuido, titulo, lead.Id, autorId);
            return;
        }

        var motivo = await configuracoes.DistribuicaoAutomaticaAsync(ct)
            ? "ninguém está no rodízio"
            : "a distribuição automática está desligada";
        await notificador.ParaAdminsAsync(TipoNotificacao.LeadSemResponsavel, $"Lead novo sem responsável: {motivo}", lead.Id, autorId, ct);
    }

    /// <summary>
    /// A pessoa já era um lead e entrou de novo: avisa a responsável ou, se o lead está sem responsável, os
    /// admins. Quando uma atendente cadastra à mão uma pessoa que é lead de outra pessoa (ela não vê esse lead),
    /// os admins também são avisados (seção 3.2).
    /// </summary>
    /// <param name="origem">A origem da nova entrada.</param>
    /// <param name="campanha">O nome da campanha da nova entrada.</param>
    /// <param name="autorId">Quem cadastrou a entrada à mão. Nulo quando ela veio da integração.</param>
    public async Task EntradaRepetidaAsync(Lead lead, Origem origem, string? campanha, Guid? autorId, CancellationToken ct)
    {
        if (lead.Teste)
        {
            return;
        }

        await RegistrarEventoAsync(TipoEvento.LeadEntradaRepetida, lead, new DadosEntradaRepetida(origem, campanha), autorId, ct);
        var autor = await AutorAsync(autorId, ct);
        var responsavel = await ResponsavelAsync(lead, ct);
        var cadastroDeOutraPessoa = autor is { EhAdmin: false } && lead.ResponsavelId != autorId;
        if (cadastroDeOutraPessoa)
        {
            await notificador.ParaAdminsAsync(
                TipoNotificacao.CadastroRepetido,
                $"{autor!.Nome} cadastrou à mão uma pessoa que já é {DeQuemE(responsavel)}",
                lead.Id,
                autorId,
                ct);
        }

        if (responsavel is { Ativo: true })
        {
            var titulo = autor is null
                ? "Nova entrada em um lead seu"
                : $"{autor.Nome} cadastrou à mão uma pessoa que já é um lead seu";
            notificador.ParaUsuario(responsavel.Id, TipoNotificacao.NovaEntrada, titulo, lead.Id, autorId);
        }
        else if (!cadastroDeOutraPessoa)
        {
            var titulo = (autor, responsavel) switch
            {
                (null, null) => "Nova entrada em um lead sem responsável",
                (null, _) => $"Nova entrada no lead de {responsavel.Nome} (usuário desativado)",
                _ => $"{autor.Nome} cadastrou à mão uma pessoa que já é {DeQuemE(responsavel)}",
            };
            await notificador.ParaAdminsAsync(TipoNotificacao.NovaEntrada, titulo, lead.Id, autorId, ct);
        }
    }

    /// <summary>Troca de responsável: avisa a nova responsável, a não ser que ela seja quem fez a troca.</summary>
    /// <param name="anteriorId">Quem era a responsável. Nulo quando o lead estava sem responsável.</param>
    public async Task ReatribuidoAsync(Lead lead, Guid? anteriorId, Guid? autorId, CancellationToken ct)
    {
        if (lead.Teste)
        {
            return;
        }

        var anterior = await db.Users.Where(u => u.Id == anteriorId).Select(u => new PessoaNoEvento(u.Id, u.Nome)).SingleOrDefaultAsync(ct);
        await RegistrarEventoAsync(TipoEvento.LeadReatribuido, lead, new DadosReatribuido(anterior), autorId, ct);
        var autor = await AutorAsync(autorId, ct);
        var titulo = autor is null ? "Um lead passou para você" : $"{autor.Nome} passou um lead para você";
        notificador.ParaUsuario(lead.ResponsavelId, TipoNotificacao.LeadReatribuido, titulo, lead.Id, autorId);
    }

    /// <summary>O evento para o n8n, a não ser que quem fez a ação seja a própria responsável: ela já sabe.</summary>
    private Task RegistrarEventoAsync(TipoEvento tipo, Lead lead, object dados, Guid? autorId, CancellationToken ct) =>
        autorId is not null && autorId == lead.ResponsavelId ? Task.CompletedTask : eventos.RegistrarAsync(tipo, lead, dados, ct);

    /// <summary><c>um lead sem responsável</c>, <c>lead de Adriana</c> ou <c>lead de Pietra (usuário desativado)</c>.</summary>
    private static string DeQuemE(Pessoa? responsavel) => responsavel switch
    {
        null => "um lead sem responsável",
        { Ativo: true } => $"lead de {responsavel.Nome}",
        _ => $"lead de {responsavel.Nome} (usuário desativado)",
    };

    private Task<Pessoa?> ResponsavelAsync(Lead lead, CancellationToken ct) =>
        db.Users.Where(u => u.Id == lead.ResponsavelId).Select(u => new Pessoa(u.Id, u.Nome, u.Ativo, false)).SingleOrDefaultAsync(ct);

    private async Task<Pessoa?> AutorAsync(Guid? autorId, CancellationToken ct) =>
        autorId is null
            ? null
            : await db.Users
                .Where(u => u.Id == autorId)
                .Select(u => new Pessoa(u.Id, u.Nome, u.Ativo, db.Admins().Any(a => a.Id == u.Id)))
                .SingleAsync(ct);

    private sealed record Pessoa(Guid Id, string Nome, bool Ativo, bool EhAdmin);
}
