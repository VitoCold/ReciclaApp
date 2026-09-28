using System.Net.Http.Json;
using Recicla.Shared.Contracts;

namespace ReciclaApp.Services;

public interface IRetiroApiClient
{
    Task<IReadOnlyCollection<RetiroListItemDto>> ListarAsync(CancellationToken cancellationToken = default);
    Task<RetiroDetalleDto> ObtenerAsync(Guid retiroId, CancellationToken cancellationToken = default);
    Task<RetiroDetalleDto> CrearAsync(CrearRetiroRequest request, CancellationToken cancellationToken = default);
}

public sealed class RetiroApiClient(HttpClient httpClient) : IRetiroApiClient
{
    public async Task<IReadOnlyCollection<RetiroListItemDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync("api/retiros", cancellationToken);
        return await ReadAsync<List<RetiroListItemDto>>(response, cancellationToken);
    }

    public async Task<RetiroDetalleDto> ObtenerAsync(Guid retiroId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"api/retiros/{retiroId}", cancellationToken);
        return await ReadAsync<RetiroDetalleDto>(response, cancellationToken);
    }

    public async Task<RetiroDetalleDto> CrearAsync(CrearRetiroRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("api/retiros", request, cancellationToken);
        return await ReadAsync<RetiroDetalleDto>(response, cancellationToken);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
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
