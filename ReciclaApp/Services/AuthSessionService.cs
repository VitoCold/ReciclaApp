using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
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

public sealed class AuthSessionService(
    IReciclaApiClient apiClient,
    ILogger<AuthSessionService> logger) : IAuthSessionService
{
    private const string SessionTokenKey = "recicla_access_token";

    public UsuarioDto? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser is not null;

    public async Task<LoginResponse> LoginAsync(string usuario, string clave, CancellationToken cancellationToken = default)
    {
        var response = await apiClient.LoginAsync(new LoginRequest(usuario.Trim(), clave), cancellationToken);
        apiClient.SetAccessToken(response.AccessToken);
        CurrentUser = response.Usuario;

        try
        {
            await SecureStorage.Default.SetAsync(SessionTokenKey, response.AccessToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "La sesión inició, pero no pudo persistirse localmente.");
        }

        return response;
    }

    public async Task<bool> RestoreSessionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await SecureStorage.Default.GetAsync(SessionTokenKey);
            if (string.IsNullOrWhiteSpace(token))
                return false;

            apiClient.SetAccessToken(token);

            try
            {
                CurrentUser = await apiClient.MeAsync(cancellationToken);
                return true;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                await LogoutAsync();
                return false;
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "No fue posible validar la sesión almacenada contra la API.");
                return false;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo restaurar la sesión almacenada.");
            return false;
        }
    }

    public Task LogoutAsync()
    {
        apiClient.SetAccessToken(null);
        CurrentUser = null;

        try
        {
            SecureStorage.Default.Remove(SessionTokenKey);
            SecureStorage.Default.Remove("recicla_usuario");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo limpiar completamente la sesión local.");
        }

        return Task.CompletedTask;
    }
}
