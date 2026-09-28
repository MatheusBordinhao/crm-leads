using System.Net.Http.Json;
using Crm.Api.Features.Usuarios;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests.Infra;

/// <summary>Usuários e sessões para os testes. Cada teste cria os próprios usuários, com e-mail único.</summary>
public static class UsuariosDeTeste
{
    public const string SenhaPadrao = "senha-de-teste-123";

    /// <summary>Cliente em https, para o cookie Secure voltar nas requisições seguintes.</summary>
    public static HttpClient CriarClienteHttps(this WebApplicationFactory<Program> fabrica) =>
        fabrica.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    public static async Task<Usuario> CriarUsuarioAsync(
        this WebApplicationFactory<Program> fabrica,
        Perfil perfil = Perfil.Atendente,
        bool ativo = true,
        bool trocarSenha = false,
        string senha = SenhaPadrao)
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var email = $"usuario-{Guid.NewGuid():N}@example.com";
        var usuario = new Usuario
        {
            UserName = email,
            Email = email,
            Nome = $"{perfil} de teste",
            Ativo = ativo,
            TrocarSenha = trocarSenha,
            CriadoEm = DateTimeOffset.UtcNow,
        };

        Garantir(await usuarios.CreateAsync(usuario, senha));
        Garantir(await usuarios.AddToRoleAsync(usuario, perfil.ToString()));
        return usuario;
    }

    public static Task<HttpResponseMessage> PostLoginAsync(this HttpClient cliente, string? email, string senha) =>
        cliente.PostAsJsonAsync("/api/auth/login", new { email, senha }, TestContext.Current.CancellationToken);

    /// <summary>Faz login e devolve o cliente com o cookie da sessão.</summary>
    public static async Task<HttpClient> LogarAsync(
        this WebApplicationFactory<Program> fabrica, Usuario usuario, string senha = SenhaPadrao)
    {
        var cliente = fabrica.CriarClienteHttps();
        var resposta = await cliente.PostLoginAsync(usuario.Email, senha);
        resposta.EnsureSuccessStatusCode();
        return cliente;
    }

    public static async Task<HttpClient> LogarComoAsync(this WebApplicationFactory<Program> fabrica, Perfil perfil) =>
        await fabrica.LogarAsync(await fabrica.CriarUsuarioAsync(perfil));

    public static async Task<Usuario> BuscarUsuarioAsync(this WebApplicationFactory<Program> fabrica, Guid id)
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        return await usuarios.FindByIdAsync(id.ToString()) ?? throw new InvalidOperationException("Usuário não encontrado.");
    }

    private static void Garantir(IdentityResult resultado)
    {
        if (!resultado.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", resultado.Errors.Select(e => e.Description)));
        }
    }
}
