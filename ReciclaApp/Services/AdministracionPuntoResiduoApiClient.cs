using System.Net.Http.Json;
using Recicla.Shared.Contracts;

namespace ReciclaApp.Services;

public interface IAdministracionPuntoResiduoApiClient
{
    Task<IReadOnlyCollection<AdministracionPuntoResiduoDto>> ListarAsync(CancellationToken cancellationToken = default);
    Task<AdministracionPuntoResiduoDto> ObtenerAsync(Guid puntoResiduoId, CancellationToken cancellationToken = default);
    Task<AdministracionPuntosCatalogosDto> ObtenerCatalogosAsync(CancellationToken cancellationToken = default);
    Task<AdministracionPuntoResiduoDto> CrearAsync(
        GuardarAdministracionPuntoResiduoRequest request,
        CancellationToken cancellationToken = default);
    Task<AdministracionPuntoResiduoDto> ActualizarAsync(
        Guid puntoResiduoId,
        GuardarAdministracionPuntoResiduoRequest request,
        CancellationToken cancellationToken = default);
    Task<AdministracionPuntoResiduoDto> CambiarEstadoAsync(
        Guid puntoResiduoId,
        bool esActivo,
        CancellationToken cancellationToken = default);
}

public sealed class AdministracionPuntoResiduoApiClient(HttpClient httpClient) : IAdministracionPuntoResiduoApiClient
{
    private const string BasePath = "api/administracion/puntos-residuo";

    public async Task<IReadOnlyCollection<AdministracionPuntoResiduoDto>> ListarAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(BasePath, cancellationToken);
        return await ReadAsync<AdministracionPuntoResiduoDto[]>(response, cancellationToken);
    }

    public async Task<AdministracionPuntoResiduoDto> ObtenerAsync(
        Guid puntoResiduoId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{BasePath}/{puntoResiduoId}", cancellationToken);
        return await ReadAsync<AdministracionPuntoResiduoDto>(response, cancellationToken);
    }

    public async Task<AdministracionPuntosCatalogosDto> ObtenerCatalogosAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{BasePath}/catalogos", cancellationToken);
        return await ReadAsync<AdministracionPuntosCatalogosDto>(response, cancellationToken);
    }

    public async Task<AdministracionPuntoResiduoDto> CrearAsync(
        GuardarAdministracionPuntoResiduoRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(BasePath, request, cancellationToken);
        return await ReadAsync<AdministracionPuntoResiduoDto>(response, cancellationToken);
    }

    public async Task<AdministracionPuntoResiduoDto> ActualizarAsync(
        Guid puntoResiduoId,
        GuardarAdministracionPuntoResiduoRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"{BasePath}/{puntoResiduoId}", request, cancellationToken);
        return await ReadAsync<AdministracionPuntoResiduoDto>(response, cancellationToken);
    }

    public async Task<AdministracionPuntoResiduoDto> CambiarEstadoAsync(
        Guid puntoResiduoId,
        bool esActivo,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PatchAsJsonAsync(
            $"{BasePath}/{puntoResiduoId}/estado",
            new CambiarEstadoAdministracionPuntoResiduoRequest(esActivo),
            cancellationToken);
        return await ReadAsync<AdministracionPuntoResiduoDto>(response, cancellationToken);
    }

    private static async Task<T> ReadAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"La API respondió {(int)response.StatusCode} ({response.ReasonPhrase}). {body}",
                null,
                response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
            ?? throw new HttpRequestException("La API devolvió una respuesta vacía.");
    }
}
