using System.Net.Http.Json;
using Recicla.Shared.Contracts;

namespace ReciclaApp.Services;

public interface IInventarioApiClient
{
    Task<InventarioResiduosDto> ObtenerAsync(CancellationToken cancellationToken = default);
    Task<InventarioResiduoItemDto> AlmacenarAsync(
        Guid registroResiduoId,
        AlmacenarResiduoRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class InventarioApiClient(HttpClient httpClient) : IInventarioApiClient
{
    public async Task<InventarioResiduosDto> ObtenerAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync("api/inventario-residuos", cancellationToken);
        return await ReadAsync<InventarioResiduosDto>(response, cancellationToken);
    }

    public async Task<InventarioResiduoItemDto> AlmacenarAsync(
        Guid registroResiduoId,
        AlmacenarResiduoRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"api/inventario-residuos/{registroResiduoId}/almacenar",
            request,
            cancellationToken);
        return await ReadAsync<InventarioResiduoItemDto>(response, cancellationToken);
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
