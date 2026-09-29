using System.Text.Json.Serialization;
using Crm.Api.Infra.Web;
using FluentValidation;

namespace Crm.Api.Features.Leads;

public sealed record UsuarioResumo(Guid Id, string Nome);

/// <summary>O que aconteceu, em cada item da linha do tempo.</summary>
public enum TipoItemLinhaDoTempo
{
    [JsonStringEnumMemberName("entrada")]
    Entrada = 1,

    [JsonStringEnumMemberName("interacao")]
    Interacao = 2,

    [JsonStringEnumMemberName("etapa")]
    Etapa = 3,

    [JsonStringEnumMemberName("responsavel")]
    Responsavel = 4,
}

/// <param name="Situacao"><c>criado</c> na primeira entrada; <c>vinculado</c> quando a pessoa voltou a entrar.</param>
public sealed record EntradaNaLinhaDoTempo(Origem Origem, CampanhaResumo? Campanha, SituacaoEntrada Situacao, bool Teste);

/// <param name="CriadoEm">Quando foi registrada, que pode ser depois de quando aconteceu.</param>
public sealed record InteracaoNaLinhaDoTempo(long Id, TipoInteracao Tipo, string? Descricao, DateTimeOffset CriadoEm);

public sealed record MudancaEtapaNaLinhaDoTempo(
    EtapaResumo Anterior, EtapaResumo Nova, MotivoPerdaResumo? MotivoPerda, string? MotivoPerdaDetalhe);

/// <param name="Anterior">Nulo quando o lead estava sem responsável.</param>
/// <param name="Nova">Nulo quando o lead ficou sem responsável.</param>
public sealed record TrocaResponsavelNaLinhaDoTempo(UsuarioResumo? Anterior, UsuarioResumo? Nova);

/// <summary>
/// Um item da linha do tempo da ficha (seção 3.5). Só o objeto do <see cref="Tipo"/> vem preenchido; os outros
/// vêm nulos.
/// </summary>
/// <param name="OcorridoEm">Quando aconteceu. Na entrada, quando o CRM a recebeu.</param>
/// <param name="Usuario">Quem fez. Nulo quando foi o sistema, como numa entrada da integração.</param>
public sealed record ItemLinhaDoTempo(
    TipoItemLinhaDoTempo Tipo,
    DateTimeOffset OcorridoEm,
    UsuarioResumo? Usuario,
    EntradaNaLinhaDoTempo? Entrada,
    InteracaoNaLinhaDoTempo? Interacao,
    MudancaEtapaNaLinhaDoTempo? Etapa,
    TrocaResponsavelNaLinhaDoTempo? Responsavel);

/// <param name="Descricao">Obrigatória na nota. Nos outros tipos, pode ficar vazia.</param>
/// <param name="OcorridoEm">Quando aconteceu: a tela sugere a hora atual e aceita datas passadas, nunca futuras.</param>
public sealed record RegistrarInteracaoRequest(TipoInteracao Tipo, string? Descricao, DateTimeOffset OcorridoEm)
{
    /// <summary>Sem a descrição, que é dado pessoal: o MVC imprime os argumentos da action no log de nível Trace.</summary>
    public override string ToString() => $"{nameof(RegistrarInteracaoRequest)} {{ Tipo = {Tipo}, OcorridoEm = {OcorridoEm:O} }}";
}

internal sealed class RegistrarInteracaoRequestValidator : Validador<RegistrarInteracaoRequest>
{
    /// <summary>Folga para o relógio do navegador adiantado: até isso no futuro, a interação vale como agora.</summary>
    public static readonly TimeSpan ToleranciaRelogio = TimeSpan.FromMinutes(5);

    public const string MensagemDataFutura = "A data não pode estar no futuro.";

    public RegistrarInteracaoRequestValidator(TimeProvider relogio)
    {
        RuleFor(r => r.Tipo).IsInEnum().WithMessage("Escolha o tipo da interação.");
        RuleFor(r => r.Descricao)
            .Must(d => !string.IsNullOrWhiteSpace(d))
            .When(r => r.Tipo == TipoInteracao.Nota)
            .WithMessage("Escreva a nota.");
        RuleFor(r => r.Descricao)
            .Must(d => d is null || d.Trim().Length <= Interacao.TamanhoMaximoDescricao)
            .WithMessage($"A descrição pode ter até {Interacao.TamanhoMaximoDescricao} caracteres.");
        RuleFor(r => r.OcorridoEm)
            .Must(quando => quando <= relogio.GetUtcNow() + ToleranciaRelogio)
            .WithMessage(MensagemDataFutura);
    }
}
