using System.Net.Http.Json;
using Recicla.Shared.Contracts;

namespace ReciclaApp.Services;

public interface IDisposicionFinalApiClient
{
    Task<IReadOnlyCollection<TipoTratamientoDto>> ListarTiposTratamientoAsync(CancellationToken cancellationToken = default);
    Task<DisposicionFinalDto> ObtenerPorRetiroAsync(Guid retiroId, CancellationToken cancellationToken = default);
    Task<DisposicionFinalDto> CrearAsync(Guid retiroId, CrearDisposicionFinalRequest request, CancellationToken cancellationToken = default);
    Task<DisposicionFinalDto> SubirEvidenciaAsync(
        Guid disposicionFinalId,
        Stream stream,
        string fileName,
        string contentType,
        string tipoEvidencia,
        CancellationToken cancellationToken = default);
    Task<DisposicionFinalDto> ValidarAsync(Guid disposicionFinalId, CancellationToken cancellationToken = default);
}

public sealed class DisposicionFinalApiClient(HttpClient httpClient) : IDisposicionFinalApiClient
{
    public async Task<IReadOnlyCollection<TipoTratamientoDto>> ListarTiposTratamientoAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync("api/disposiciones-finales/tipos-tratamiento", cancellationToken);
        return await ReadAsync<List<TipoTratamientoDto>>(response, cancellationToken);
    }

    public async Task<DisposicionFinalDto> ObtenerPorRetiroAsync(Guid retiroId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"api/disposiciones-finales/retiro/{retiroId}", cancellationToken);
        return await ReadAsync<DisposicionFinalDto>(response, cancellationToken);
    }

    public async Task<DisposicionFinalDto> CrearAsync(
        Guid retiroId,
        CrearDisposicionFinalRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"api/disposiciones-finales/retiro/{retiroId}",
            request,
            cancellationToken);
        return await ReadAsync<DisposicionFinalDto>(response, cancellationToken);
    }

    public async Task<DisposicionFinalDto> SubirEvidenciaAsync(
        Guid disposicionFinalId,
        Stream stream,
        string fileName,
        string contentType,
        string tipoEvidencia,
        CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        using var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        form.Add(streamContent, "file", fileName);
        form.Add(new StringContent(tipoEvidencia), "tipoEvidencia");

        var response = await httpClient.PostAsync(
            $"api/disposiciones-finales/{disposicionFinalId}/evidencias",
            form,
            cancellationToken);
        return await ReadAsync<DisposicionFinalDto>(response, cancellationToken);
    }

    public async Task<DisposicionFinalDto> ValidarAsync(Guid disposicionFinalId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync(
            $"api/disposiciones-finales/{disposicionFinalId}/validar",
            null,
            cancellationToken);
        return await ReadAsync<DisposicionFinalDto>(response, cancellationToken);
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
