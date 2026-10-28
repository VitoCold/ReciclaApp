using System.Net.Http.Json;
using Recicla.Shared.Contracts;

namespace ReciclaApp.Services;

public interface IAdministracionSedeApiClient
{
    Task<IReadOnlyCollection<AdministracionSedeDto>> ListarAsync(CancellationToken cancellationToken = default);
    Task<AdministracionSedeDto> ObtenerAsync(Guid sedeId, CancellationToken cancellationToken = default);
    Task<AdministracionSedeDto> CrearAsync(
        GuardarAdministracionSedeRequest request,
        CancellationToken cancellationToken = default);
    Task<AdministracionSedeDto> ActualizarAsync(
        Guid sedeId,
        GuardarAdministracionSedeRequest request,
        CancellationToken cancellationToken = default);
    Task<AdministracionSedeDto> CambiarEstadoAsync(
        Guid sedeId,
        bool esActivo,
        CancellationToken cancellationToken = default);
}

public sealed class AdministracionSedeApiClient(HttpClient httpClient) : IAdministracionSedeApiClient
{
    private const string BasePath = "api/administracion/sedes";

    public async Task<IReadOnlyCollection<AdministracionSedeDto>> ListarAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(BasePath, cancellationToken);
        return await ReadAsync<AdministracionSedeDto[]>(response, cancellationToken);
    }

    public async Task<AdministracionSedeDto> ObtenerAsync(
        Guid sedeId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{BasePath}/{sedeId}", cancellationToken);
        return await ReadAsync<AdministracionSedeDto>(response, cancellationToken);
    }

    public async Task<AdministracionSedeDto> CrearAsync(
        GuardarAdministracionSedeRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(BasePath, request, cancellationToken);
        return await ReadAsync<AdministracionSedeDto>(response, cancellationToken);
    }

    public async Task<AdministracionSedeDto> ActualizarAsync(
        Guid sedeId,
        GuardarAdministracionSedeRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"{BasePath}/{sedeId}", request, cancellationToken);
        return await ReadAsync<AdministracionSedeDto>(response, cancellationToken);
    }

    public async Task<AdministracionSedeDto> CambiarEstadoAsync(
        Guid sedeId,
        bool esActivo,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PatchAsJsonAsync(
            $"{BasePath}/{sedeId}/estado",
            new CambiarEstadoAdministracionSedeRequest(esActivo),
            cancellationToken);
        return await ReadAsync<AdministracionSedeDto>(response, cancellationToken);
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
