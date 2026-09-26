using System.Net.Http.Json;
using Recicla.Shared.Contracts;

namespace ReciclaApp.Services;

public interface IRegistroResiduoEvidenciaApiClient
{
    Task<IReadOnlyCollection<RegistroResiduoEvidenciaDto>> ListarAsync(
        Guid registroId,
        CancellationToken cancellationToken = default);

    Task<RegistroResiduoEvidenciaDto> GuardarUbicacionAsync(
        Guid registroId,
        Guid registroResiduoId,
        GuardarUbicacionResiduoRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class RegistroResiduoEvidenciaApiClient(HttpClient httpClient) : IRegistroResiduoEvidenciaApiClient
{
    public async Task<IReadOnlyCollection<RegistroResiduoEvidenciaDto>> ListarAsync(
        Guid registroId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            $"api/registros/{registroId}/evidencias-residuos",
            cancellationToken);
        return await ReadAsync<List<RegistroResiduoEvidenciaDto>>(response, cancellationToken);
    }

    public async Task<RegistroResiduoEvidenciaDto> GuardarUbicacionAsync(
        Guid registroId,
        Guid registroResiduoId,
        GuardarUbicacionResiduoRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"api/registros/{registroId}/evidencias-residuos/{registroResiduoId}/ubicacion",
            request,
            cancellationToken);
        return await ReadAsync<RegistroResiduoEvidenciaDto>(response, cancellationToken);
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
