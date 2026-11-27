using System.Net.Http.Json;
using Recicla.Shared.Contracts;

namespace ReciclaApp.Services;

public interface IMonitoreoApiClient
{
    Task<MonitoreoActividadDto> ObtenerActividadAsync(
        MonitoreoActividadFiltroRequest filtro,
        CancellationToken cancellationToken = default);
}

public sealed class MonitoreoApiClient(HttpClient httpClient) : IMonitoreoApiClient
{
    public async Task<MonitoreoActividadDto> ObtenerActividadAsync(
        MonitoreoActividadFiltroRequest filtro,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>();

        if (filtro.DesdeUtc.HasValue)
            query.Add($"desdeUtc={Uri.EscapeDataString(filtro.DesdeUtc.Value.ToString("O"))}");
        if (filtro.HastaUtc.HasValue)
            query.Add($"hastaUtc={Uri.EscapeDataString(filtro.HastaUtc.Value.ToString("O"))}");
        if (filtro.SedeId.HasValue)
            query.Add($"sedeId={filtro.SedeId.Value}");
        if (filtro.ClasificacionResiduoId.HasValue)
            query.Add($"clasificacionResiduoId={filtro.ClasificacionResiduoId.Value}");
        if (!string.IsNullOrWhiteSpace(filtro.EstadoOperativo))
            query.Add($"estadoOperativo={Uri.EscapeDataString(filtro.EstadoOperativo)}");
        if (!string.IsNullOrWhiteSpace(filtro.Buscar))
            query.Add($"buscar={Uri.EscapeDataString(filtro.Buscar)}");

        query.Add($"limite={Math.Clamp(filtro.Limite, 1, 200)}");

        var url = "api/monitoreo/actividad?" + string.Join("&", query);
        var response = await httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"La API respondió {(int)response.StatusCode} ({response.ReasonPhrase}). {body}",
                null,
                response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<MonitoreoActividadDto>(cancellationToken: cancellationToken)
            ?? throw new HttpRequestException("La API devolvió una respuesta vacía.");
    }
}
