using System.Reflection;
using System.Runtime.CompilerServices;
using Crm.Api.Features.Leads;

namespace Crm.UnitTests.Infra;

public class RequisicoesNoLogTests
{
    /// <summary>
    /// O MVC imprime os argumentos da action no log de nível Trace, pelo ToString. O ToString que o compilador
    /// gera para um record imprime todos os campos (nome, e-mail, senha), então todo record de requisição,
    /// inclusive os dos próximos passos, precisa do seu.
    /// </summary>
    [Fact]
    public void Todo_record_de_requisicao_tem_um_ToString_sem_os_dados()
    {
        var requisicoes = typeof(NovaEntrada).Assembly.GetTypes()
            .Where(t => t.Name.EndsWith("Request", StringComparison.Ordinal) && t.GetMethod("<Clone>$") is not null)
            .ToList();

        var comToStringGerado = requisicoes
            .Where(t => t.GetMethod(nameof(ToString), Type.EmptyTypes)!.GetCustomAttribute<CompilerGeneratedAttribute>() is not null)
            .Select(t => t.Name);

        Assert.NotEmpty(requisicoes);
        Assert.Empty(comToStringGerado);
    }
}
