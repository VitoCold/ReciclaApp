using System.Net.Http.Json;
using Recicla.Shared.Contracts;

namespace ReciclaApp.Services;

public interface IControlGeneracionWorkflowClient
{
    Task<RegistroControlDetalleDto> CompletarRegistroAsync(
        Guid controlId,
        Guid registroId,
        CancellationToken cancellationToken = default);
}

public sealed class ControlGeneracionWorkflowClient(HttpClient httpClient) : IControlGeneracionWorkflowClient
{
    public async Task<RegistroControlDetalleDto> CompletarRegistroAsync(
        Guid controlId,
        Guid registroId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync(
            $"api/controles-generacion/{controlId}/registros/{registroId}/completar",
            null,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"La API respondió {(int)response.StatusCode} ({response.ReasonPhrase}). {body}",
                null,
                response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<RegistroControlDetalleDto>(cancellationToken: cancellationToken)
            ?? throw new HttpRequestException("La API devolvió una respuesta vacía.");
    }
}
