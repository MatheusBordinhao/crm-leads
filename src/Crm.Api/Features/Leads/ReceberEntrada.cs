using System.Globalization;
using System.Text.Json;
using Crm.Api.Features.Funil;
using Crm.Api.Features.Leads.Normalizacao;
using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Dados;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Features.Leads;

/// <summary>
/// Porta de entrada de todo lead (seção 3.2): integração, cadastro manual e importação. Numa transação só,
/// confere se a entrada já foi recebida, procura a pessoa entre os leads, vincula ou cria e grava a entrada.
/// </summary>
public sealed class ReceberEntrada(
    CrmDbContext db,
    AuditoriaService auditoria,
    MudancaEtapa mudancaEtapa,
    TimeProvider relogio,
    ILogger<ReceberEntrada> logger)
{
    public async Task<EntradaProcessada> ExecutarAsync(NovaEntrada entrada, CancellationToken ct)
    {
        var pessoa = DadosPessoa.De(entrada);
        var idExterno = Textos.Limpar(entrada.IdExterno);
        var referenciaCampanha = Normalizar(entrada.Campanha);

        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        await TravarAsync(ChavesDeTrava(entrada.Origem, idExterno, pessoa, referenciaCampanha?.Id), ct);

        if (idExterno is not null && await LeadDaEntradaAnteriorAsync(entrada.Origem, idExterno, ct) is { } leadAnterior)
        {
            logger.LogInformation("Entrada já recebida da origem {Origem}: lead {LeadId}", entrada.Origem, leadAnterior);
            return new EntradaProcessada(leadAnterior, ResultadoEntrada.JaRecebido);
        }

        var agora = relogio.GetUtcNow();
        var campanha = await ResolverCampanhaAsync(entrada.Origem, referenciaCampanha, ct);

        Lead lead;
        ResultadoEntrada resultado;
        if (await BuscarPessoaAsync(pessoa, entrada.Teste, ct) is { } existenteId)
        {
            lead = await CarregarParaAlterarAsync(existenteId, ct);
            await VincularAsync(lead, pessoa, entrada.UsuarioId, agora, ct);
            resultado = ResultadoEntrada.Vinculado;
        }
        else
        {
            lead = await CriarLeadAsync(entrada, pessoa, campanha, agora, ct);
            resultado = ResultadoEntrada.Criado;
        }

        // Gera o id do lead novo, usado pela entrada e pela auditoria.
        await db.SaveChangesAsync(ct);

        var registro = CriarEntradaRecebida(entrada, idExterno, lead.Id, campanha, resultado, agora);
        db.EntradasRecebidas.Add(registro);
        if (resultado == ResultadoEntrada.Criado)
        {
            auditoria.Registrar(
                AcoesAuditoria.LeadCriado,
                EntidadesAuditoria.Lead,
                lead.Id.ToString(CultureInfo.InvariantCulture),
                new { origem = EnumTexto<Origem>.Nome(entrada.Origem) },
                entrada.UsuarioId);
        }

        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);

        logger.LogInformation(
            "Entrada {EntradaId} da origem {Origem}: lead {LeadId} {Resultado}", registro.Id, entrada.Origem, lead.Id, resultado);
        return new EntradaProcessada(lead.Id, resultado);
    }

    /// <summary>
    /// Chaves das travas: a da entrada (idempotência), as da pessoa (deduplicação) e a da campanha (criação
    /// única). Duas entradas que compartilham uma chave rodam uma depois da outra.
    /// </summary>
    private static IEnumerable<string> ChavesDeTrava(Origem origem, string? idExterno, DadosPessoa pessoa, string? campanhaId)
    {
        var nomeOrigem = EnumTexto<Origem>.Nome(origem);
        if (idExterno is not null)
        {
            yield return $"entrada:{nomeOrigem}:{idExterno}";
        }

        if (pessoa.DocumentoValido)
        {
            yield return $"documento:{pessoa.CpfCnpj}";
        }

        if (pessoa.EmailValido)
        {
            yield return $"email:{pessoa.Email}";
        }

        if (pessoa.TelefoneValido)
        {
            yield return $"telefone:{pessoa.Telefone}";
        }

        if (campanhaId is not null)
        {
            yield return $"campanha:{nomeOrigem}:{campanhaId}";
        }
    }

    /// <summary>
    /// <c>pg_advisory_xact_lock</c>, que dura até o fim da transação. As chaves são pegas sempre em ordem
    /// alfabética, para duas entradas nunca ficarem esperando uma pela outra.
    /// </summary>
    private async Task TravarAsync(IEnumerable<string> chaves, CancellationToken ct)
    {
        foreach (var chave in chaves.Distinct().Order(StringComparer.Ordinal))
        {
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({chave}, 0))", ct);
        }
    }

    private Task<long?> LeadDaEntradaAnteriorAsync(Origem origem, string idExterno, CancellationToken ct) =>
        db.EntradasRecebidas
            .Where(e => e.Origem == origem && e.IdExterno == idExterno && e.Situacao != SituacaoEntrada.Erro)
            .Select(e => e.LeadId)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Campanha desconhecida é criada, com um nome provisório se vier sem nome. Um nome que chegue depois só
    /// substitui o provisório.
    /// </summary>
    private async Task<Campanha?> ResolverCampanhaAsync(Origem origem, ReferenciaExterna? referencia, CancellationToken ct)
    {
        if (referencia?.Id is not { } idExterno)
        {
            return null;
        }

        var campanha = await db.Campanhas.SingleOrDefaultAsync(c => c.Origem == origem && c.IdExterno == idExterno, ct);
        if (campanha is null)
        {
            campanha = new Campanha
            {
                Origem = origem,
                IdExterno = idExterno,
                Nome = referencia.Nome ?? Campanha.GerarNomeProvisorio(origem, idExterno),
                NomeProvisorio = referencia.Nome is null,
            };
            db.Campanhas.Add(campanha);
            await db.SaveChangesAsync(ct);
        }
        else if (campanha.NomeProvisorio && referencia.Nome is not null)
        {
            campanha.Nome = referencia.Nome;
            campanha.NomeProvisorio = false;
        }

        return campanha;
    }

    /// <summary>
    /// Procura a pessoa por CPF/CNPJ, e-mail e telefone, nessa ordem, cada um só quando está em formato válido.
    /// Ignora leads anonimizados, e uma entrada de teste só encontra leads de teste (e vice-versa).
    /// </summary>
    private async Task<long?> BuscarPessoaAsync(DadosPessoa pessoa, bool teste, CancellationToken ct)
    {
        var candidatos = db.Leads.Where(l => l.AnonimizadoEm == null && l.Teste == teste).OrderBy(l => l.Id);

        if (pessoa.DocumentoValido
            && await candidatos.Where(l => l.CpfCnpj == pessoa.CpfCnpj).Select(l => (long?)l.Id).FirstOrDefaultAsync(ct) is { } porDocumento)
        {
            return porDocumento;
        }

        if (pessoa.EmailValido
            && await candidatos.Where(l => l.Email == pessoa.Email).Select(l => (long?)l.Id).FirstOrDefaultAsync(ct) is { } porEmail)
        {
            return porEmail;
        }

        if (pessoa.TelefoneValido)
        {
            return await candidatos.Where(l => l.Telefone == pessoa.Telefone).Select(l => (long?)l.Id).FirstOrDefaultAsync(ct);
        }

        return null;
    }

    /// <summary>
    /// Trava a linha do lead até o fim da transação e o carrega. Uma edição feita ao mesmo tempo pela tela
    /// espera e depois recebe o conflito de versão (409), em vez de esta entrada falhar.
    /// </summary>
    private async Task<Lead> CarregarParaAlterarAsync(long id, CancellationToken ct)
    {
        await db.TravarLeadAsync(id, ct);
        return await db.Leads.SingleAsync(l => l.Id == id, ct);
    }

    /// <summary>
    /// A pessoa já é um lead: preenche só os campos vazios. Se o lead estava perdido, volta para a etapa
    /// inicial; se estava em ganho, continua em ganho.
    /// </summary>
    private async Task VincularAsync(Lead lead, DadosPessoa pessoa, Guid? usuarioId, DateTimeOffset agora, CancellationToken ct)
    {
        lead.Email ??= pessoa.Email;
        lead.Telefone ??= pessoa.Telefone;
        lead.CpfCnpj ??= pessoa.CpfCnpj;
        lead.Uf ??= pessoa.Uf;
        lead.Empresa ??= pessoa.Empresa;
        lead.AtualizadoEm = agora;

        var tipoAtual = await db.Etapas.Where(e => e.Id == lead.EtapaId).Select(e => e.Tipo).SingleAsync(ct);
        if (tipoAtual != TipoEtapa.Perdido)
        {
            return;
        }

        // Voltar a entrar não é um contato da atendente.
        var inicial = await db.Etapas.InicialAsync(ct);
        mudancaEtapa.Registrar(lead, inicial, inicial.Id, motivo: null, detalhe: null, usuarioId, agora, contaComoContato: false);
    }

    private async Task<Lead> CriarLeadAsync(
        NovaEntrada entrada, DadosPessoa pessoa, Campanha? campanha, DateTimeOffset agora, CancellationToken ct)
    {
        var lead = new Lead
        {
            Nome = pessoa.Nome,
            Email = pessoa.Email,
            Telefone = pessoa.Telefone,
            CpfCnpj = pessoa.CpfCnpj,
            Uf = pessoa.Uf,
            Empresa = pessoa.Empresa,
            EtapaId = (await db.Etapas.InicialAsync(ct)).Id,
            ResponsavelId = entrada.Teste ? null : entrada.ResponsavelId,
            Origem = entrada.Origem,
            Plataforma = Textos.LimparECortar(entrada.Plataforma, Lead.TamanhoMaximoPlataforma)?.ToLowerInvariant(),
            CampanhaId = campanha?.Id,
            UtmSource = Rastreio(entrada.Utm?.Source),
            UtmMedium = Rastreio(entrada.Utm?.Medium),
            UtmCampaign = Rastreio(entrada.Utm?.Campaign),
            UtmContent = Rastreio(entrada.Utm?.Content),
            UtmTerm = Rastreio(entrada.Utm?.Term),
            Gclid = Rastreio(entrada.Gclid),
            RespostasExtras = ComoDocumento(entrada.RespostasExtras),
            Teste = entrada.Teste,
            CriadoEm = agora,
            AtualizadoEm = agora,
        };
        db.Leads.Add(lead);
        return lead;
    }

    private static EntradaRecebida CriarEntradaRecebida(
        NovaEntrada entrada, string? idExterno, long leadId, Campanha? campanha, ResultadoEntrada resultado, DateTimeOffset agora) =>
        new()
        {
            LeadId = leadId,
            Origem = entrada.Origem,
            IdExterno = idExterno,
            CampanhaId = campanha?.Id,
            Conjunto = ComoDocumento(Normalizar(entrada.Conjunto)),
            Anuncio = ComoDocumento(Normalizar(entrada.Anuncio)),
            Formulario = ComoDocumento(Normalizar(entrada.Formulario)),
            UtmSource = Rastreio(entrada.Utm?.Source),
            UtmMedium = Rastreio(entrada.Utm?.Medium),
            UtmCampaign = Rastreio(entrada.Utm?.Campaign),
            UtmContent = Rastreio(entrada.Utm?.Content),
            UtmTerm = Rastreio(entrada.Utm?.Term),
            Gclid = Rastreio(entrada.Gclid),
            RespostasExtras = ComoDocumento(entrada.RespostasExtras),
            PayloadBruto = entrada.PayloadBruto,
            Situacao = resultado == ResultadoEntrada.Criado ? SituacaoEntrada.Criado : SituacaoEntrada.Vinculado,
            Teste = entrada.Teste,
            RecebidoEm = entrada.RecebidoEm.ToUniversalTime(),
            ProcessadoEm = agora,
        };

    private static ReferenciaExterna? Normalizar(ReferenciaExterna? referencia)
    {
        var id = Textos.LimparECortar(referencia?.Id, Campanha.TamanhoMaximoIdExterno);
        var nome = Textos.LimparECortar(referencia?.Nome, Campanha.TamanhoMaximoNome);
        return id is null && nome is null ? null : new ReferenciaExterna(id, nome);
    }

    private static string? Rastreio(string? valor) => Textos.LimparECortar(valor, Lead.TamanhoMaximoRastreio);

    private static JsonDocument? ComoDocumento(ReferenciaExterna? referencia) =>
        referencia is null ? null : JsonSerializer.SerializeToDocument(referencia, JsonSerializerOptions.Web);

    private static JsonDocument? ComoDocumento(IReadOnlyDictionary<string, JsonElement>? respostas) =>
        respostas is { Count: > 0 } ? JsonSerializer.SerializeToDocument(respostas) : null;
}

internal static class LeadsExtensions
{
    public static IServiceCollection AddLeads(this IServiceCollection services) =>
        services.AddScoped<ReceberEntrada>().AddScoped<VisibilidadeLeads>();
}
