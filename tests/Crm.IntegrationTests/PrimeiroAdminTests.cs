using Crm.Api.Features.Usuarios;
using Crm.IntegrationTests.Infra;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests;

public class PrimeiroAdminTests(CrmApiFactory fabrica)
{
    [Fact]
    public async Task Primeiro_admin_e_criado_na_subida_com_perfil_admin_e_troca_de_senha_pendente()
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<Usuario>>();

        var admin = await usuarios.FindByEmailAsync(CrmApiFactory.AdminInicialEmail);

        Assert.NotNull(admin);
        Assert.Equal(InicializacaoAcesso.NomeAdminInicial, admin.Nome);
        Assert.True(admin.Ativo);
        Assert.True(admin.TrocarSenha);
        Assert.True(await usuarios.IsInRoleAsync(admin, nameof(Perfil.Admin)));
        Assert.True(await usuarios.CheckPasswordAsync(admin, CrmApiFactory.AdminInicialSenha));
    }

    [Fact]
    public async Task Com_admin_cadastrado_a_inicializacao_nao_cria_outro()
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var antes = await usuarios.FindByEmailAsync(CrmApiFactory.AdminInicialEmail);

        // Se tentasse criar o admin de novo, falharia com o e-mail duplicado.
        await escopo.ServiceProvider.GetRequiredService<InicializacaoAcesso>().ExecutarAsync();

        var depois = await usuarios.FindByEmailAsync(CrmApiFactory.AdminInicialEmail);
        Assert.Equal(antes?.Id, depois?.Id);
    }

    [Fact]
    public async Task Os_dois_perfis_existem()
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var perfis = escopo.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var perfil in Enum.GetNames<Perfil>())
        {
            Assert.True(await perfis.RoleExistsAsync(perfil), perfil);
        }
    }
}
