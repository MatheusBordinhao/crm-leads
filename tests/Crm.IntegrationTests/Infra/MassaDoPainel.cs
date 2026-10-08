using Crm.Api.Features.Funil;
using Crm.Api.Features.Leads;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Dados;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Crm.IntegrationTests.Infra;

/// <summary>
/// Massa de dados conhecida do painel (critério de aceite do Passo 8), num banco só dela: assim, nenhum outro teste
/// mexe nos totais do admin. É setembro de 2026, gravado direto no banco, com as horas no horário de Brasília (UTC−3).
/// </summary>
/// <remarks>
/// <code>
/// Lead  Responsável  Origem e campanha  Criado em (Brasília)  Hoje
/// A1    Adriana      Meta, C1           01/09 00:00           Ganho em 05/09; contato em 10 min
/// A2    Adriana      Meta, C1           10/09                 Em contato; 30 min; nova entrada do site, sem campanha, em 21/09
/// A3    Adriana      Google, C2         15/09                 Perdido (Preço) em 16/09; 60 min
/// A4    Adriana      manual             30/09 23:59           Novo; sem contato
/// X1    Adriana      Meta, C1           05/09                 Perdido (Sem resposta) em 08/09; 90 min; anonimizado
/// P1    Pietra       Google, C2         12/09                 Ganho em 14/09; 20 min
/// P2    Pietra       site               20/09                 Novo; sem contato
/// S1    ninguém      Meta, C1           25/09                 Novo; sem contato
/// O1    Adriana      Meta, C1           31/08 23:59:59        Perdido (Sem interesse) em 03/09; reaberto em 18/09 por uma
///                                       (01/09 em UTC)        entrada do Google em C2, recebida pela origem em 20/08
/// O2    Pietra       Google, C2         01/10 00:00           Novo; a origem recebeu às 23:50 de 30/09
/// T1    teste        Meta, C1           10/09                 Perdido (Preço) em 11/09; mais uma entrada de teste em 12/09
/// </code>
/// A Pietra está desativada. Em setembro, o admin vê 8 leads novos (A1 a S1, menos O1, O2 e T1), 10 entradas (as 8
/// dos leads novos, a do site em A2 e a do Google em O1), 2 ganhos e as perdas de A3, X1 e O1.
/// </remarks>
public sealed class MassaDoPainel
{
    /// <summary>O período da massa, na query string do painel.</summary>
    public const string Setembro = "de=2026-09-01&ate=2026-09-30";

    private static readonly SemaphoreSlim CriacaoDaMassa = new(1, 1);

    private static MassaDoPainel? _massa;

    public required string Banco { get; init; }

    public required Usuario Adriana { get; init; }

    /// <summary>Desativada: os leads continuam contando para ela.</summary>
    public required Usuario Pietra { get; init; }

    /// <summary>Campanha da Meta.</summary>
    public required Campanha C1 { get; init; }

    /// <summary>Campanha do Google.</summary>
    public required Campanha C2 { get; init; }

    /// <summary>A API no banco da massa.</summary>
    public WebApplicationFactory<Program> CriarApi(CrmApiFactory fabrica) => CriarApi(fabrica, Banco);

    /// <summary>A massa, criada uma vez e lida pelos testes do painel e do relatório. Nenhum teste a muda.</summary>
    public static async Task<MassaDoPainel> ObterAsync(CrmApiFactory fabrica)
    {
        await CriacaoDaMassa.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            return _massa ??= await CriarAsync(fabrica);
        }
        finally
        {
            CriacaoDaMassa.Release();
        }
    }

    private static async Task<MassaDoPainel> CriarAsync(CrmApiFactory fabrica)
    {
        var ct = TestContext.Current.CancellationToken;
        var banco = await fabrica.CriarBancoAsync("painel");
        Usuario adriana;
        Usuario pietra;
        using (var api = CriarApi(fabrica, banco))
        {
            adriana = await api.CriarUsuarioAsync(nome: "Adriana");
            pietra = await api.CriarUsuarioAsync(ativo: false, nome: "Pietra");
        }

        await using var db = new CrmDbContext(DadosExtensions.CriarOpcoes(banco));
        var etapas = await db.Etapas.ToDictionaryAsync(e => e.Nome, e => e.Id, ct);
        var motivos = await db.MotivosPerda.ToDictionaryAsync(m => m.Nome, m => m.Id, ct);
        var c1 = new Campanha { Origem = Origem.MetaLeadAds, IdExterno = "c1", Nome = "Setembro | Serviço Y" };
        var c2 = new Campanha { Origem = Origem.GoogleAds, IdExterno = "c2", Nome = "Google | Serviço Z" };
        db.Campanhas.AddRange(c1, c2);

        Lead NovoLead(Usuario? responsavel, Origem origem, Campanha? campanha, DateTimeOffset criadoEm, string etapa, int? minutosAteOContato = null) => new()
        {
            Nome = "Lead da massa",
            Email = LeadsDeTeste.EmailUnico(),
            EtapaId = etapas[etapa],
            ResponsavelId = responsavel?.Id,
            Origem = origem,
            Campanha = campanha,
            CriadoEm = criadoEm,
            AtualizadoEm = criadoEm,
            PrimeiroContatoEm = minutosAteOContato is { } minutos ? criadoEm.AddMinutes(minutos) : null,
        };

        var a1 = NovoLead(adriana, Origem.MetaLeadAds, c1, Brasilia(9, 1, 0, 0), "Ganho", 10);
        a1.ConvertidoEm = Brasilia(9, 5);
        var a2 = NovoLead(adriana, Origem.MetaLeadAds, c1, Brasilia(9, 10), "Em contato", 30);
        var a3 = NovoLead(adriana, Origem.GoogleAds, c2, Brasilia(9, 15), "Perdido", 60);
        a3.PerdidoEm = Brasilia(9, 16);
        a3.MotivoPerdaId = motivos["Preço"];
        var a4 = NovoLead(adriana, Origem.Manual, null, Brasilia(9, 30, 23, 59), "Novo");
        var x1 = NovoLead(adriana, Origem.MetaLeadAds, c1, Brasilia(9, 5), "Perdido", 90);
        x1.PerdidoEm = Brasilia(9, 8);
        x1.MotivoPerdaId = motivos["Sem resposta"];
        x1.Nome = "Titular removido";
        x1.Email = null;
        x1.AnonimizadoEm = Brasilia(10, 2);
        var p1 = NovoLead(pietra, Origem.GoogleAds, c2, Brasilia(9, 12), "Ganho", 20);
        p1.ConvertidoEm = Brasilia(9, 14);
        var p2 = NovoLead(pietra, Origem.Site, null, Brasilia(9, 20), "Novo");
        var s1 = NovoLead(null, Origem.MetaLeadAds, c1, Brasilia(9, 25), "Novo");
        var o1 = NovoLead(adriana, Origem.MetaLeadAds, c1, Brasilia(8, 31, 23, 59, 59), "Novo");
        o1.PrimeiroContatoEm = Brasilia(9, 3);
        var o2 = NovoLead(pietra, Origem.GoogleAds, c2, Brasilia(10, 1, 0, 0), "Novo");
        var t1 = NovoLead(null, Origem.MetaLeadAds, c1, Brasilia(9, 10), "Perdido");
        t1.Teste = true;
        t1.PerdidoEm = Brasilia(9, 11);
        t1.MotivoPerdaId = motivos["Preço"];
        db.Leads.AddRange(a1, a2, a3, a4, x1, p1, p2, s1, o1, o2, t1);
        await db.SaveChangesAsync(ct);

        // A primeira entrada de cada lead é a que o criou, processada na hora em que ele foi criado.
        EntradaRecebida Criacao(Lead lead, DateTimeOffset? recebidoEm = null) =>
            Entrada(lead, lead.Origem, lead.Campanha, lead.CriadoEm, SituacaoEntrada.Criado, recebidoEm);

        db.EntradasRecebidas.AddRange(
            Criacao(a1),
            Criacao(a2),
            Criacao(a3),
            Criacao(a4),
            Criacao(x1),
            Criacao(p1),
            Criacao(p2),
            Criacao(s1),
            Criacao(o1),
            Criacao(o2, recebidoEm: Brasilia(9, 30, 23, 50)),
            Criacao(t1),
            Entrada(a2, Origem.Site, null, Brasilia(9, 21), SituacaoEntrada.Vinculado),
            Entrada(o1, Origem.GoogleAds, c2, Brasilia(9, 18), SituacaoEntrada.Vinculado, recebidoEm: Brasilia(8, 20)),
            Entrada(t1, Origem.MetaLeadAds, c1, Brasilia(9, 12), SituacaoEntrada.Vinculado));

        // Quem mudou foi a responsável; a reabertura por uma nova entrada é do sistema.
        HistoricoEtapa Mudanca(Lead lead, string de, string para, DateTimeOffset quando, string? motivo = null, bool peloSistema = false) => new()
        {
            LeadId = lead.Id,
            EtapaAnteriorId = etapas[de],
            EtapaNovaId = etapas[para],
            MotivoPerdaId = motivo is null ? null : motivos[motivo],
            UsuarioId = peloSistema ? null : lead.ResponsavelId,
            OcorridoEm = quando,
        };

        db.HistoricoEtapas.AddRange(
            Mudanca(a1, "Novo", "Ganho", Brasilia(9, 5)),
            Mudanca(a2, "Novo", "Em contato", Brasilia(9, 10, 12, 30)),
            Mudanca(a3, "Novo", "Perdido", Brasilia(9, 16), "Preço"),
            Mudanca(x1, "Novo", "Perdido", Brasilia(9, 8), "Sem resposta"),
            Mudanca(p1, "Novo", "Ganho", Brasilia(9, 14)),
            Mudanca(o1, "Novo", "Perdido", Brasilia(9, 3), "Sem interesse"),
            Mudanca(o1, "Perdido", "Novo", Brasilia(9, 18), peloSistema: true),
            Mudanca(t1, "Novo", "Perdido", Brasilia(9, 11), "Preço"));
        await db.SaveChangesAsync(ct);

        return new MassaDoPainel { Banco = banco, Adriana = adriana, Pietra = pietra, C1 = c1, C2 = c2 };
    }

    private static WebApplicationFactory<Program> CriarApi(CrmApiFactory fabrica, string banco) =>
        fabrica.WithWebHostBuilder(builder => builder.UseSetting($"ConnectionStrings:{DadosExtensions.NomeConexao}", banco));

    /// <summary>Um instante de 2026 no horário de Brasília, que não tem horário de verão desde 2019.</summary>
    private static DateTimeOffset Brasilia(int mes, int dia, int hora = 12, int minuto = 0, int segundo = 0) =>
        new DateTimeOffset(2026, mes, dia, hora, minuto, segundo, TimeSpan.FromHours(-3)).ToUniversalTime();

    private static EntradaRecebida Entrada(
        Lead lead, Origem origem, Campanha? campanha, DateTimeOffset processadoEm, SituacaoEntrada situacao, DateTimeOffset? recebidoEm = null) => new()
        {
            LeadId = lead.Id,
            Origem = origem,
            IdExterno = origem == Origem.Manual ? null : LeadsDeTeste.IdExternoUnico(),
            CampanhaId = campanha?.Id,
            Situacao = situacao,
            Teste = lead.Teste,
            RecebidoEm = recebidoEm ?? processadoEm,
            ProcessadoEm = processadoEm,
        };
}
