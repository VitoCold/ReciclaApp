using System.Net.Http.Json;
using Recicla.Shared.Contracts;

namespace ReciclaApp.Services;

public interface IAdministracionUsuarioApiClient
{
    Task<IReadOnlyCollection<AdministracionUsuarioDto>> ListarAsync(CancellationToken cancellationToken = default);
    Task<AdministracionUsuarioDto> ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken = default);
    Task<AdministracionUsuariosCatalogosDto> ObtenerCatalogosAsync(CancellationToken cancellationToken = default);
    Task<AdministracionUsuarioDto> CrearAsync(
        GuardarAdministracionUsuarioRequest request,
        CancellationToken cancellationToken = default);
    Task<AdministracionUsuarioDto> ActualizarAsync(
        Guid usuarioId,
        GuardarAdministracionUsuarioRequest request,
        CancellationToken cancellationToken = default);
    Task<AdministracionUsuarioDto> CambiarEstadoAsync(
        Guid usuarioId,
        bool esActivo,
        CancellationToken cancellationToken = default);
    Task RestablecerClaveAsync(
        Guid usuarioId,
        string password,
        CancellationToken cancellationToken = default);
}

public sealed class AdministracionUsuarioApiClient(HttpClient httpClient) : IAdministracionUsuarioApiClient
{
    private const string BasePath = "api/administracion/usuarios";

    public async Task<IReadOnlyCollection<AdministracionUsuarioDto>> ListarAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(BasePath, cancellationToken);
        return await ReadAsync<AdministracionUsuarioDto[]>(response, cancellationToken);
    }

    public async Task<AdministracionUsuarioDto> ObtenerAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{BasePath}/{usuarioId}", cancellationToken);
        return await ReadAsync<AdministracionUsuarioDto>(response, cancellationToken);
    }

    public async Task<AdministracionUsuariosCatalogosDto> ObtenerCatalogosAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{BasePath}/catalogos", cancellationToken);
        return await ReadAsync<AdministracionUsuariosCatalogosDto>(response, cancellationToken);
    }

    public async Task<AdministracionUsuarioDto> CrearAsync(
        GuardarAdministracionUsuarioRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(BasePath, request, cancellationToken);
        return await ReadAsync<AdministracionUsuarioDto>(response, cancellationToken);
    }

    public async Task<AdministracionUsuarioDto> ActualizarAsync(
        Guid usuarioId,
        GuardarAdministracionUsuarioRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"{BasePath}/{usuarioId}", request, cancellationToken);
        return await ReadAsync<AdministracionUsuarioDto>(response, cancellationToken);
    }

    public async Task<AdministracionUsuarioDto> CambiarEstadoAsync(
        Guid usuarioId,
        bool esActivo,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PatchAsJsonAsync(
            $"{BasePath}/{usuarioId}/estado",
            new CambiarEstadoAdministracionUsuarioRequest(esActivo),
            cancellationToken);
        return await ReadAsync<AdministracionUsuarioDto>(response, cancellationToken);
    }

    public async Task RestablecerClaveAsync(
        Guid usuarioId,
        string password,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"{BasePath}/{usuarioId}/restablecer-clave",
            new RestablecerClaveAdministracionUsuarioRequest(password),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            await ThrowAsync(response, cancellationToken);
    }

    private static async Task<T> ReadAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
            await ThrowAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
            ?? throw new HttpRequestException("La API devolvió una respuesta vacía.");
    }

    private static async Task ThrowAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"La API respondió {(int)response.StatusCode} ({response.ReasonPhrase}). {body}",
            null,
            response.StatusCode);
    }
}
