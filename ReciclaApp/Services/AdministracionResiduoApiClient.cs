using System.Net.Http.Json;
using Recicla.Shared.Contracts;

namespace ReciclaApp.Services;

public interface IAdministracionResiduoApiClient
{
    Task<IReadOnlyCollection<AdministracionTipoResiduoDto>> ListarTiposAsync(CancellationToken cancellationToken = default);
    Task<AdministracionTipoResiduoDto> ObtenerTipoAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AdministracionTipoResiduoDto> CrearTipoAsync(GuardarAdministracionTipoResiduoRequest request, CancellationToken cancellationToken = default);
    Task<AdministracionTipoResiduoDto> ActualizarTipoAsync(Guid id, GuardarAdministracionTipoResiduoRequest request, CancellationToken cancellationToken = default);
    Task<AdministracionTipoResiduoDto> CambiarEstadoTipoAsync(Guid id, bool esActivo, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AdministracionResiduoCatalogoDto>> ListarResiduosAsync(CancellationToken cancellationToken = default);
    Task<AdministracionResiduoCatalogoDto> ObtenerResiduoAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AdministracionResiduosCatalogosDto> ObtenerCatalogosAsync(CancellationToken cancellationToken = default);
    Task<AdministracionResiduoCatalogoDto> CrearResiduoAsync(GuardarAdministracionResiduoCatalogoRequest request, CancellationToken cancellationToken = default);
    Task<AdministracionResiduoCatalogoDto> ActualizarResiduoAsync(Guid id, GuardarAdministracionResiduoCatalogoRequest request, CancellationToken cancellationToken = default);
    Task<AdministracionResiduoCatalogoDto> CambiarEstadoResiduoAsync(Guid id, bool esActivo, CancellationToken cancellationToken = default);
}

public sealed class AdministracionResiduoApiClient(HttpClient httpClient) : IAdministracionResiduoApiClient
{
    private const string BasePath = "api/administracion/residuos";

    public async Task<IReadOnlyCollection<AdministracionTipoResiduoDto>> ListarTiposAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<AdministracionTipoResiduoDto[]>($"{BasePath}/tipos", cancellationToken);

    public Task<AdministracionTipoResiduoDto> ObtenerTipoAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<AdministracionTipoResiduoDto>($"{BasePath}/tipos/{id}", cancellationToken);

    public Task<AdministracionTipoResiduoDto> CrearTipoAsync(GuardarAdministracionTipoResiduoRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<GuardarAdministracionTipoResiduoRequest, AdministracionTipoResiduoDto>(HttpMethod.Post, $"{BasePath}/tipos", request, cancellationToken);

    public Task<AdministracionTipoResiduoDto> ActualizarTipoAsync(Guid id, GuardarAdministracionTipoResiduoRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<GuardarAdministracionTipoResiduoRequest, AdministracionTipoResiduoDto>(HttpMethod.Put, $"{BasePath}/tipos/{id}", request, cancellationToken);

    public Task<AdministracionTipoResiduoDto> CambiarEstadoTipoAsync(Guid id, bool esActivo, CancellationToken cancellationToken = default) =>
        SendAsync<CambiarEstadoAdministracionTipoResiduoRequest, AdministracionTipoResiduoDto>(HttpMethod.Patch, $"{BasePath}/tipos/{id}/estado", new(esActivo), cancellationToken);

    public async Task<IReadOnlyCollection<AdministracionResiduoCatalogoDto>> ListarResiduosAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<AdministracionResiduoCatalogoDto[]>(BasePath, cancellationToken);

    public Task<AdministracionResiduoCatalogoDto> ObtenerResiduoAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<AdministracionResiduoCatalogoDto>($"{BasePath}/{id}", cancellationToken);

    public Task<AdministracionResiduosCatalogosDto> ObtenerCatalogosAsync(CancellationToken cancellationToken = default) =>
        GetAsync<AdministracionResiduosCatalogosDto>($"{BasePath}/catalogos", cancellationToken);

    public Task<AdministracionResiduoCatalogoDto> CrearResiduoAsync(GuardarAdministracionResiduoCatalogoRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<GuardarAdministracionResiduoCatalogoRequest, AdministracionResiduoCatalogoDto>(HttpMethod.Post, BasePath, request, cancellationToken);

    public Task<AdministracionResiduoCatalogoDto> ActualizarResiduoAsync(Guid id, GuardarAdministracionResiduoCatalogoRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<GuardarAdministracionResiduoCatalogoRequest, AdministracionResiduoCatalogoDto>(HttpMethod.Put, $"{BasePath}/{id}", request, cancellationToken);

    public Task<AdministracionResiduoCatalogoDto> CambiarEstadoResiduoAsync(Guid id, bool esActivo, CancellationToken cancellationToken = default) =>
        SendAsync<CambiarEstadoAdministracionResiduoCatalogoRequest, AdministracionResiduoCatalogoDto>(HttpMethod.Patch, $"{BasePath}/{id}/estado", new(esActivo), cancellationToken);

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(path, cancellationToken);
        return await ReadAsync<T>(response, cancellationToken);
    }

    private async Task<TResponse> SendAsync<TRequest,TResponse>(HttpMethod method, string path, TRequest request, CancellationToken cancellationToken)
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
            throw new HttpRequestException($"La API respondió {(int)response.StatusCode} ({response.ReasonPhrase}). {body}", null, response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
            ?? throw new HttpRequestException("La API devolvió una respuesta vacía.");
    }
}
