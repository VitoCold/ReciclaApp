using System.Net.Http.Json;
using Recicla.Shared.Contracts;

namespace ReciclaApp.Services;

public interface IAdministracionProyectoActividadApiClient
{
    Task<IReadOnlyCollection<AdministracionProyectoDto>> ListarProyectosAsync(CancellationToken cancellationToken = default);
    Task<AdministracionProyectoDto> ObtenerProyectoAsync(Guid proyectoId, CancellationToken cancellationToken = default);
    Task<AdministracionProyectoDto> CrearProyectoAsync(GuardarAdministracionProyectoRequest request, CancellationToken cancellationToken = default);
    Task<AdministracionProyectoDto> ActualizarProyectoAsync(Guid proyectoId, GuardarAdministracionProyectoRequest request, CancellationToken cancellationToken = default);
    Task<AdministracionProyectoDto> CambiarEstadoProyectoAsync(Guid proyectoId, bool esActivo, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AdministracionActividadDto>> ListarActividadesAsync(CancellationToken cancellationToken = default);
    Task<AdministracionActividadDto> ObtenerActividadAsync(Guid actividadId, CancellationToken cancellationToken = default);
    Task<AdministracionActividadesCatalogosDto> ObtenerCatalogosActividadesAsync(CancellationToken cancellationToken = default);
    Task<AdministracionActividadDto> CrearActividadAsync(GuardarAdministracionActividadRequest request, CancellationToken cancellationToken = default);
    Task<AdministracionActividadDto> ActualizarActividadAsync(Guid actividadId, GuardarAdministracionActividadRequest request, CancellationToken cancellationToken = default);
    Task<AdministracionActividadDto> CambiarEstadoActividadAsync(Guid actividadId, bool esActivo, CancellationToken cancellationToken = default);
}

public sealed class AdministracionProyectoActividadApiClient(HttpClient httpClient) : IAdministracionProyectoActividadApiClient
{
    private const string BasePath = "api/administracion";

    public async Task<IReadOnlyCollection<AdministracionProyectoDto>> ListarProyectosAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<AdministracionProyectoDto[]>($"{BasePath}/proyectos", cancellationToken);

    public Task<AdministracionProyectoDto> ObtenerProyectoAsync(Guid proyectoId, CancellationToken cancellationToken = default) =>
        GetAsync<AdministracionProyectoDto>($"{BasePath}/proyectos/{proyectoId}", cancellationToken);

    public Task<AdministracionProyectoDto> CrearProyectoAsync(GuardarAdministracionProyectoRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<GuardarAdministracionProyectoRequest, AdministracionProyectoDto>(HttpMethod.Post, $"{BasePath}/proyectos", request, cancellationToken);

    public Task<AdministracionProyectoDto> ActualizarProyectoAsync(Guid proyectoId, GuardarAdministracionProyectoRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<GuardarAdministracionProyectoRequest, AdministracionProyectoDto>(HttpMethod.Put, $"{BasePath}/proyectos/{proyectoId}", request, cancellationToken);

    public Task<AdministracionProyectoDto> CambiarEstadoProyectoAsync(Guid proyectoId, bool esActivo, CancellationToken cancellationToken = default) =>
        SendAsync<CambiarEstadoAdministracionProyectoRequest, AdministracionProyectoDto>(
            HttpMethod.Patch,
            $"{BasePath}/proyectos/{proyectoId}/estado",
            new CambiarEstadoAdministracionProyectoRequest(esActivo),
            cancellationToken);

    public async Task<IReadOnlyCollection<AdministracionActividadDto>> ListarActividadesAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<AdministracionActividadDto[]>($"{BasePath}/actividades", cancellationToken);

    public Task<AdministracionActividadDto> ObtenerActividadAsync(Guid actividadId, CancellationToken cancellationToken = default) =>
        GetAsync<AdministracionActividadDto>($"{BasePath}/actividades/{actividadId}", cancellationToken);

    public Task<AdministracionActividadesCatalogosDto> ObtenerCatalogosActividadesAsync(CancellationToken cancellationToken = default) =>
        GetAsync<AdministracionActividadesCatalogosDto>($"{BasePath}/actividades/catalogos", cancellationToken);

    public Task<AdministracionActividadDto> CrearActividadAsync(GuardarAdministracionActividadRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<GuardarAdministracionActividadRequest, AdministracionActividadDto>(HttpMethod.Post, $"{BasePath}/actividades", request, cancellationToken);

    public Task<AdministracionActividadDto> ActualizarActividadAsync(Guid actividadId, GuardarAdministracionActividadRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<GuardarAdministracionActividadRequest, AdministracionActividadDto>(HttpMethod.Put, $"{BasePath}/actividades/{actividadId}", request, cancellationToken);

    public Task<AdministracionActividadDto> CambiarEstadoActividadAsync(Guid actividadId, bool esActivo, CancellationToken cancellationToken = default) =>
        SendAsync<CambiarEstadoAdministracionActividadRequest, AdministracionActividadDto>(
            HttpMethod.Patch,
            $"{BasePath}/actividades/{actividadId}/estado",
            new CambiarEstadoAdministracionActividadRequest(esActivo),
            cancellationToken);

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(path, cancellationToken);
        return await ReadAsync<T>(response, cancellationToken);
    }

    private async Task<TResponse> SendAsync<TRequest, TResponse>(
        HttpMethod method,
        string path,
        TRequest request,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(request)
        };
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
