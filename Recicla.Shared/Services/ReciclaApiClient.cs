using System.Net.Http.Headers;
using System.Net.Http.Json;
using Recicla.Shared.Contracts;

namespace Recicla.Shared.Services;

public interface IReciclaApiClient
{
    void SetAccessToken(string? accessToken);
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<UsuarioDto> MeAsync(CancellationToken cancellationToken = default);
    Task<CatalogosInicialDto> ObtenerCatalogosAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<RegistroListItemDto>> ListarRegistrosAsync(CancellationToken cancellationToken = default);
    Task<RegistroDetalleDto> ObtenerRegistroAsync(Guid registroId, CancellationToken cancellationToken = default);
    Task<RegistroDetalleDto> CrearRegistroAsync(CrearRegistroRequest request, CancellationToken cancellationToken = default);
    Task<RegistroDetalleDto> ActualizarRegistroAsync(Guid registroId, ActualizarRegistroRequest request, CancellationToken cancellationToken = default);
    Task<RegistroResiduoDto> AgregarResiduoAsync(Guid registroId, AgregarRegistroResiduoRequest request, CancellationToken cancellationToken = default);
    Task<RegistroResiduoDto> ActualizarResiduoAsync(Guid registroId, Guid registroResiduoId, ActualizarRegistroResiduoRequest request, CancellationToken cancellationToken = default);
    Task EliminarResiduoAsync(Guid registroId, Guid registroResiduoId, CancellationToken cancellationToken = default);
    Task<RegistroResiduoFotoDto> SubirFotoAsync(Guid registroId, Guid registroResiduoId, Stream content, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task<RegistroDetalleDto> CompletarRegistroAsync(Guid registroId, CancellationToken cancellationToken = default);
    Task<DisposicionDto> GuardarDisposicionAsync(Guid registroId, CrearDisposicionRequest request, CancellationToken cancellationToken = default);
    Task<DisposicionEvidenciaDto> SubirEvidenciaAsync(Guid disposicionId, Stream content, string fileName, string contentType, string tipoEvidencia, CancellationToken cancellationToken = default);
}

public sealed class ReciclaApiClient(HttpClient httpClient) : IReciclaApiClient
{
    public void SetAccessToken(string? accessToken)
    {
        httpClient.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(accessToken)
            ? null
            : new AuthenticationHeaderValue("Bearer", accessToken);
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("api/auth/login", request, cancellationToken);
        return await ReadAsync<LoginResponse>(response, cancellationToken);
    }

    public async Task<UsuarioDto> MeAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<UsuarioDto>("api/auth/me", cancellationToken);

    public async Task<CatalogosInicialDto> ObtenerCatalogosAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<CatalogosInicialDto>("api/catalogos/inicial", cancellationToken);

    public async Task<IReadOnlyCollection<RegistroListItemDto>> ListarRegistrosAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<List<RegistroListItemDto>>("api/registros", cancellationToken);

    public async Task<RegistroDetalleDto> ObtenerRegistroAsync(Guid registroId, CancellationToken cancellationToken = default) =>
        await GetAsync<RegistroDetalleDto>($"api/registros/{registroId}", cancellationToken);

    public async Task<RegistroDetalleDto> CrearRegistroAsync(CrearRegistroRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("api/registros", request, cancellationToken);
        return await ReadAsync<RegistroDetalleDto>(response, cancellationToken);
    }

    public async Task<RegistroDetalleDto> ActualizarRegistroAsync(Guid registroId, ActualizarRegistroRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"api/registros/{registroId}", request, cancellationToken);
        return await ReadAsync<RegistroDetalleDto>(response, cancellationToken);
    }

    public async Task<RegistroResiduoDto> AgregarResiduoAsync(Guid registroId, AgregarRegistroResiduoRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"api/registros/{registroId}/residuos", request, cancellationToken);
        return await ReadAsync<RegistroResiduoDto>(response, cancellationToken);
    }

    public async Task<RegistroResiduoDto> ActualizarResiduoAsync(Guid registroId, Guid registroResiduoId, ActualizarRegistroResiduoRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"api/registros/{registroId}/residuos/{registroResiduoId}", request, cancellationToken);
        return await ReadAsync<RegistroResiduoDto>(response, cancellationToken);
    }

    public async Task EliminarResiduoAsync(Guid registroId, Guid registroResiduoId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"api/registros/{registroId}/residuos/{registroResiduoId}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<RegistroResiduoFotoDto> SubirFotoAsync(
        Guid registroId,
        Guid registroResiduoId,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        using var multipart = new MultipartFormDataContent();
        using var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(streamContent, "file", fileName);

        var response = await httpClient.PostAsync(
            $"api/registros/{registroId}/residuos/{registroResiduoId}/fotos",
            multipart,
            cancellationToken);
        return await ReadAsync<RegistroResiduoFotoDto>(response, cancellationToken);
    }

    public async Task<RegistroDetalleDto> CompletarRegistroAsync(Guid registroId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync($"api/registros/{registroId}/completar", null, cancellationToken);
        return await ReadAsync<RegistroDetalleDto>(response, cancellationToken);
    }

    public async Task<DisposicionDto> GuardarDisposicionAsync(Guid registroId, CrearDisposicionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"api/registros/{registroId}/disposicion", request, cancellationToken);
        return await ReadAsync<DisposicionDto>(response, cancellationToken);
    }

    public async Task<DisposicionEvidenciaDto> SubirEvidenciaAsync(
        Guid disposicionId,
        Stream content,
        string fileName,
        string contentType,
        string tipoEvidencia,
        CancellationToken cancellationToken = default)
    {
        using var multipart = new MultipartFormDataContent();
        using var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(streamContent, "file", fileName);
        multipart.Add(new StringContent(tipoEvidencia), "tipoEvidencia");

        var response = await httpClient.PostAsync($"api/disposiciones/{disposicionId}/evidencias", multipart, cancellationToken);
        return await ReadAsync<DisposicionEvidenciaDto>(response, cancellationToken);
    }

    private async Task<T> GetAsync<T>(string uri, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(uri, cancellationToken);
        return await ReadAsync<T>(response, cancellationToken);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
            ?? throw new HttpRequestException("La API devolvió una respuesta vacía.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"La API respondió {(int)response.StatusCode} ({response.ReasonPhrase}). {body}",
            null,
            response.StatusCode);
    }
}
