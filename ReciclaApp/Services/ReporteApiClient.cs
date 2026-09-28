using System.Net.Http.Json;
using Recicla.Shared.Contracts;

namespace ReciclaApp.Services;

public sealed record ReporteArchivoDescarga(
    byte[] Contenido,
    string NombreArchivo,
    string ContentType);

public interface IReporteApiClient
{
    Task<ReporteResiduosDto> ObtenerAsync(
        ReporteResiduosFiltroRequest filtro,
        CancellationToken cancellationToken = default);

    Task<ReporteArchivoDescarga> ExportarExcelAsync(
        ReporteResiduosFiltroRequest filtro,
        CancellationToken cancellationToken = default);
}

public sealed class ReporteApiClient(HttpClient httpClient) : IReporteApiClient
{
    public async Task<ReporteResiduosDto> ObtenerAsync(
        ReporteResiduosFiltroRequest filtro,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(ConstruirUri("api/reportes/residuos", filtro), cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ReporteResiduosDto>(cancellationToken: cancellationToken)
            ?? throw new HttpRequestException("La API devolvió un reporte vacío.");
    }

    public async Task<ReporteArchivoDescarga> ExportarExcelAsync(
        ReporteResiduosFiltroRequest filtro,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(ConstruirUri("api/reportes/residuos/excel", filtro), cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var contenido = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var nombre = response.Content.Headers.ContentDisposition?.FileNameStar
                     ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                     ?? $"reporte_residuos_{filtro.Desde:yyyyMMdd}_{filtro.Hasta:yyyyMMdd}.xlsx";
        var contentType = response.Content.Headers.ContentType?.MediaType
                          ?? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        return new ReporteArchivoDescarga(contenido, nombre, contentType);
    }

    private static string ConstruirUri(string baseUri, ReporteResiduosFiltroRequest filtro)
    {
        var parametros = new List<string>
        {
            $"desde={Uri.EscapeDataString(filtro.Desde.ToString("yyyy-MM-dd"))}",
            $"hasta={Uri.EscapeDataString(filtro.Hasta.ToString("yyyy-MM-dd"))}"
        };

        Agregar(parametros, "sedeId", filtro.SedeId);
        Agregar(parametros, "empresaResponsableId", filtro.EmpresaResponsableId);
        Agregar(parametros, "controlGeneracionId", filtro.ControlGeneracionId);
        Agregar(parametros, "clasificacionResiduoId", filtro.ClasificacionResiduoId);
        Agregar(parametros, "residuoId", filtro.ResiduoId);

        return $"{baseUri}?{string.Join("&", parametros)}";
    }

    private static void Agregar(ICollection<string> parametros, string nombre, Guid? valor)
    {
        if (valor.HasValue)
            parametros.Add($"{nombre}={valor.Value}");
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
