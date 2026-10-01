using Crm.Api.Features.Leads;
using Crm.Api.Infra.Web;
using FluentValidation;

namespace Crm.Api.Infra.Importacoes;

/// <param name="NomeArquivo">O nome do arquivo, só para mostrar no resultado.</param>
/// <param name="ConteudoBase64">O arquivo em base64, com até 5 MB.</param>
public sealed record AnalisarImportacaoRequest(string NomeArquivo, string ConteudoBase64)
{
    /// <summary>Sem o arquivo nem o nome dele: o MVC imprime os argumentos da action no log de nível Trace.</summary>
    public override string ToString() => $"{nameof(AnalisarImportacaoRequest)} {{ Tamanho = {ConteudoBase64?.Length} }}";
}

/// <summary>Uma coluna do arquivo e o campo sugerido para ela pelo cabeçalho.</summary>
public sealed record ColunaAnalisada(string Nome, CampoImportacao Sugestao);

/// <summary>O arquivo lido, para a tela mostrar a prévia e o mapeamento antes de importar. Nada é gravado.</summary>
/// <param name="Previa">As 10 primeiras linhas, com um valor por coluna.</param>
/// <param name="TotalLinhas">As linhas com algum valor, sem contar o cabeçalho.</param>
/// <param name="ColunaIdExterno">A coluna sugerida como id do lead na origem, contando de 0.</param>
/// <param name="Origem">A origem sugerida: <c>meta_lead_ads</c> para o arquivo da Meta; senão, <c>planilha</c>.</param>
public sealed record AnaliseImportacaoResponse(
    string NomeArquivo,
    Codificacao Codificacao,
    Separador Separador,
    IReadOnlyList<ColunaAnalisada> Colunas,
    IReadOnlyList<IReadOnlyList<string>> Previa,
    int TotalLinhas,
    int? ColunaIdExterno,
    Origem Origem);

/// <param name="Campos">O campo de cada coluna, na ordem do cabeçalho.</param>
/// <param name="ColunaIdExterno">
/// A coluna com o id do lead na origem, contando de 0. Nula: o id externo de cada linha é o hash do conteúdo dela.
/// </param>
/// <param name="Origem">Qualquer origem, menos <c>manual</c>.</param>
/// <param name="CampanhaPadraoId">Uma campanha que já existe, da mesma origem, para as linhas sem campanha.</param>
/// <param name="NovaCampanha">O nome de uma campanha nova, em vez de uma que existe.</param>
/// <param name="ResponsavelId">Quem recebe os leads novos. Nulo: o rodízio.</param>
public sealed record CriarImportacaoRequest(
    string NomeArquivo,
    string ConteudoBase64,
    IReadOnlyList<CampoImportacao> Campos,
    int? ColunaIdExterno,
    Origem Origem,
    int? CampanhaPadraoId,
    string? NovaCampanha,
    Guid? ResponsavelId)
{
    /// <summary>Sem o arquivo nem o nome dele: o MVC imprime os argumentos da action no log de nível Trace.</summary>
    public override string ToString() =>
        $"{nameof(CriarImportacaoRequest)} {{ Origem = {Origem}, Tamanho = {ConteudoBase64?.Length} }}";
}

/// <param name="Linha">A linha na planilha, contando o cabeçalho como linha 1.</param>
public sealed record ErroImportacaoResponse(int Linha, string Motivo);

/// <summary>O andamento e o resultado de uma importação.</summary>
/// <param name="CampanhaPadrao">O nome da campanha das linhas sem campanha.</param>
/// <param name="Responsavel">Quem recebe os leads novos. Nulo: o rodízio.</param>
/// <param name="ImportadoPor">O nome de quem importou.</param>
/// <param name="TotalLinhas">As linhas com algum valor, sem o cabeçalho.</param>
/// <param name="JaImportados">Linhas que já tinham entrado antes, por esta ou por outra importação.</param>
/// <param name="SemResponsavel">Dos leads criados, quantos ficaram sem responsável.</param>
/// <param name="Erros">As 100 primeiras linhas com erro. O relatório de erros traz todas.</param>
public sealed record ImportacaoResponse(
    long Id,
    string NomeArquivo,
    Codificacao Codificacao,
    Separador Separador,
    Origem Origem,
    string? CampanhaPadrao,
    ResponsavelResumo? Responsavel,
    string ImportadoPor,
    SituacaoImportacao Situacao,
    int TotalLinhas,
    int LinhasProcessadas,
    int Criados,
    int Vinculados,
    int JaImportados,
    int ComErro,
    int SemResponsavel,
    IReadOnlyList<ErroImportacaoResponse> Erros,
    DateTimeOffset CriadoEm,
    DateTimeOffset? ConcluidoEm);

internal sealed class AnalisarImportacaoRequestValidator : Validador<AnalisarImportacaoRequest>
{
    public AnalisarImportacaoRequestValidator() => RuleFor(r => r.NomeArquivo).NomeDoArquivo();
}

internal sealed class CriarImportacaoRequestValidator : Validador<CriarImportacaoRequest>
{
    public const string MensagemOrigem = "A importação aceita todas as origens, menos o cadastro manual.";

    public const string MensagemNome = "Escolha a coluna do nome.";

    public const string MensagemContato = "Escolha a coluna do e-mail ou a do telefone.";

    public const string MensagemCampoRepetido = "Cada campo, menos o nome e as respostas extras, vai para uma coluna só.";

    public const string MensagemCampanha = "Escolha uma campanha que já existe ou dê o nome de uma nova, não as duas.";

    public static readonly string MensagemNovaCampanha =
        $"O nome da campanha nova pode ter até {Campanha.TamanhoMaximoIdExterno} caracteres.";

    public CriarImportacaoRequestValidator()
    {
        RuleFor(r => r.NomeArquivo).NomeDoArquivo();
        RuleFor(r => r.Campos)
            .NotNull().WithMessage(MensagemNome)
            .Must(campos => campos.Contains(CampoImportacao.Nome)).WithMessage(MensagemNome)
            .Must(campos => campos.Contains(CampoImportacao.Email) || campos.Contains(CampoImportacao.Telefone))
            .WithMessage(MensagemContato)
            .Must(campos => !campos.Where(CamposImportacao.UmaColunaSo).GroupBy(c => c).Any(g => g.Count() > 1))
            .WithMessage(MensagemCampoRepetido);
        RuleFor(r => r.ColunaIdExterno).GreaterThanOrEqualTo(0).WithMessage("Escolha a coluna do id externo.");
        RuleFor(r => r.Origem).NotEqual(Origem.Manual).WithMessage(MensagemOrigem);
        RuleFor(r => r.NovaCampanha)
            .Must((r, nome) => r.CampanhaPadraoId is null || string.IsNullOrWhiteSpace(nome)).WithMessage(MensagemCampanha)
            .Must(nome => nome is null || nome.Trim().Length <= Campanha.TamanhoMaximoIdExterno).WithMessage(MensagemNovaCampanha);
    }
}

internal static class ValidacaoImportacao
{
    public static IRuleBuilderOptions<T, string> NomeDoArquivo<T>(this IRuleBuilder<T, string> regra) =>
        regra
            .NotEmpty().WithMessage("Envie o arquivo.")
            .Must(nome => nome.Trim().Length <= Importacao.TamanhoMaximoNomeArquivo)
            .WithMessage($"O nome do arquivo pode ter até {Importacao.TamanhoMaximoNomeArquivo} caracteres.");
}
