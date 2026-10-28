using System.Net.Http.Json;
using Recicla.Shared.Contracts;

namespace ReciclaApp.Services;

public interface IAdministracionEmpresaApiClient
{
    Task<IReadOnlyCollection<AdministracionEmpresaDto>> ListarAsync(CancellationToken cancellationToken = default);
    Task<AdministracionEmpresaDto> ObtenerAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AdministracionEmpresaDto> CrearAsync(GuardarAdministracionEmpresaRequest request, CancellationToken cancellationToken = default);
    Task<AdministracionEmpresaDto> ActualizarAsync(Guid id, GuardarAdministracionEmpresaRequest request, CancellationToken cancellationToken = default);
    Task<AdministracionEmpresaDto> CambiarEstadoAsync(Guid id, bool esActivo, CancellationToken cancellationToken = default);
}

public sealed class AdministracionEmpresaApiClient(HttpClient httpClient) : IAdministracionEmpresaApiClient
{
    private const string BasePath = "api/administracion/empresas";

    public async Task<IReadOnlyCollection<AdministracionEmpresaDto>> ListarAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<AdministracionEmpresaDto[]>(BasePath, cancellationToken);

    public Task<AdministracionEmpresaDto> ObtenerAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<AdministracionEmpresaDto>($"{BasePath}/{id}", cancellationToken);

    public Task<AdministracionEmpresaDto> CrearAsync(GuardarAdministracionEmpresaRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<GuardarAdministracionEmpresaRequest, AdministracionEmpresaDto>(HttpMethod.Post, BasePath, request, cancellationToken);

    public Task<AdministracionEmpresaDto> ActualizarAsync(Guid id, GuardarAdministracionEmpresaRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<GuardarAdministracionEmpresaRequest, AdministracionEmpresaDto>(HttpMethod.Put, $"{BasePath}/{id}", request, cancellationToken);

    public Task<AdministracionEmpresaDto> CambiarEstadoAsync(Guid id, bool esActivo, CancellationToken cancellationToken = default) =>
        SendAsync<CambiarEstadoAdministracionEmpresaRequest, AdministracionEmpresaDto>(
            HttpMethod.Patch, $"{BasePath}/{id}/estado", new(esActivo), cancellationToken);

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(path, cancellationToken);
        return await ReadAsync<T>(response, cancellationToken);
    }

    private async Task<TResponse> SendAsync<TRequest,TResponse>(
        HttpMethod method,
        string path,
        TRequest request,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(method, path) { Content = JsonContent.Create(request) };
        var response = await httpClient.SendAsync(message, cancellationToken);
        return await ReadAsync<TResponse>(response, cancellationToken);
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
