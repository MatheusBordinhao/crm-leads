using Crm.Api.Features.Leads;

namespace Crm.UnitTests.Leads;

public class RegistrarInteracaoValidatorTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 5, 17, 0, 0, TimeSpan.Zero);

    private sealed class RelogioFixo : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Agora;
    }

    private static IEnumerable<(string, string)> Erros(RegistrarInteracaoRequest requisicao) =>
        new RegistrarInteracaoRequestValidator(new RelogioFixo()).Validate(requisicao).Errors.Select(e => (e.PropertyName, e.ErrorMessage));

    [Fact]
    public void Nota_exige_texto_e_os_outros_tipos_nao()
    {
        Assert.Equal([("Descricao", "Escreva a nota.")], Erros(new RegistrarInteracaoRequest(TipoInteracao.Nota, " ", Agora)));
        Assert.Empty(Erros(new RegistrarInteracaoRequest(TipoInteracao.Ligacao, null, Agora)));
        Assert.Empty(Erros(new RegistrarInteracaoRequest(TipoInteracao.Nota, "Ligar amanhã", Agora)));
    }

    [Fact]
    public void Data_passada_vale_e_futura_so_dentro_da_folga_do_relogio()
    {
        Assert.Empty(Erros(new RegistrarInteracaoRequest(TipoInteracao.Reuniao, null, Agora.AddYears(-1))));
        Assert.Empty(Erros(new RegistrarInteracaoRequest(TipoInteracao.Reuniao, null, Agora.AddMinutes(5))));
        Assert.Equal(
            [("OcorridoEm", RegistrarInteracaoRequestValidator.MensagemDataFutura)],
            Erros(new RegistrarInteracaoRequest(TipoInteracao.Reuniao, null, Agora.AddMinutes(6))));
    }

    [Fact]
    public void Descricao_tem_limite_e_tipo_precisa_ser_valido()
    {
        Assert.Equal(
            [("Descricao", "A descrição pode ter até 2000 caracteres.")],
            Erros(new RegistrarInteracaoRequest(TipoInteracao.Email, new string('x', 2001), Agora)));
        Assert.Equal(
            [("Tipo", "Escolha o tipo da interação.")],
            Erros(new RegistrarInteracaoRequest(0, "Texto", Agora)));
    }
}
