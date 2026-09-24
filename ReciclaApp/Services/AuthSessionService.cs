using System.Globalization;
using System.Net;
using System.Text.Json;
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
    private const string SessionTokenKey = "recicla_session_token";
    private const string SessionExpirationKey = "recicla_session_expiration";
    private const string SessionUserKey = "recicla_session_user";

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
            await SecureStorage.Default.SetAsync(
                SessionExpirationKey,
                response.ExpiraUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            await SecureStorage.Default.SetAsync(SessionUserKey, JsonSerializer.Serialize(response.Usuario));
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
            var expirationText = await SecureStorage.Default.GetAsync(SessionExpirationKey);

            if (string.IsNullOrWhiteSpace(token) ||
                !DateTime.TryParse(expirationText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiration) ||
                expiration.ToUniversalTime() <= DateTime.UtcNow.AddSeconds(30))
            {
                await LogoutAsync();
                return false;
            }

            apiClient.SetAccessToken(token);
            CurrentUser = await ReadCachedUserAsync();

            try
            {
                CurrentUser = await apiClient.MeAsync(cancellationToken);
                await SecureStorage.Default.SetAsync(SessionUserKey, JsonSerializer.Serialize(CurrentUser));
                return true;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                await LogoutAsync();
                return false;
            }
            catch (HttpRequestException ex) when (CurrentUser is not null)
            {
                logger.LogWarning(ex, "No se pudo validar la sesión contra la API; se conserva la sesión local vigente.");
                return true;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo restaurar la sesión.");
        }

        await LogoutAsync();
        return false;
    }

    public Task LogoutAsync()
    {
        apiClient.SetAccessToken(null);
        CurrentUser = null;

        try
        {
            SecureStorage.Default.Remove(SessionTokenKey);
            SecureStorage.Default.Remove(SessionExpirationKey);
            SecureStorage.Default.Remove(SessionUserKey);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo limpiar completamente la sesión local.");
        }

        return Task.CompletedTask;
    }

    private static async Task<UsuarioDto?> ReadCachedUserAsync()
    {
        var json = await SecureStorage.Default.GetAsync(SessionUserKey);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<UsuarioDto>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
