using Recicla.Shared.Contracts;
using Recicla.Shared.Services;

namespace ReciclaApp.Services;

public interface IAuthSessionService
{
    UsuarioDto? CurrentUser { get; }
    bool IsAuthenticated { get; }
    Task<LoginResponse> LoginAsync(string usuario, string clave, CancellationToken cancellationToken = default);
    Task<bool> RestoreSessionAsync(CancellationToken cancellationToken = default);
    Task LogoutAsync();
}

public sealed class AuthSessionService(IReciclaApiClient apiClient) : IAuthSessionService
{
    public UsuarioDto? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser is not null;

    public async Task<LoginResponse> LoginAsync(string usuario, string clave, CancellationToken cancellationToken = default)
    {
        var response = await apiClient.LoginAsync(new LoginRequest(usuario.Trim(), clave), cancellationToken);
        apiClient.SetAccessToken(response.AccessToken);
        CurrentUser = response.Usuario;
        return response;
    }

    public Task<bool> RestoreSessionAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);

    public Task LogoutAsync()
    {
        apiClient.SetAccessToken(null);
        CurrentUser = null;
        return Task.CompletedTask;
    }
}
