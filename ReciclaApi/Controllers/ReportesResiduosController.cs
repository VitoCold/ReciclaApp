using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recicla.Shared.Contracts;
using ReciclaApi.Application.Services;

namespace ReciclaApi.Controllers;

[Authorize(Roles = "ADMINISTRADOR,AMBIENTAL,RESPONSABLE_OPERATIVO")]
[Route("api/reportes/residuos")]
public sealed class ReportesResiduosController(
    IReporteResiduosService reportesService,
    ILogger<ReportesResiduosController> logger) : BaseController<ReportesResiduosController>(logger)
{
    [HttpGet]
    public async Task<IActionResult> Obtener(
        [FromQuery] DateTime desde,
        [FromQuery] DateTime hasta,
        [FromQuery] Guid? sedeId,
        [FromQuery] Guid? empresaResponsableId,
        [FromQuery] Guid? controlGeneracionId,
        [FromQuery] int? clasificacionResiduoId,
        [FromQuery] Guid? residuoId,
        CancellationToken cancellationToken)
    {
        var filtro = new ReporteResiduosFiltroRequest(
            desde, hasta, sedeId, empresaResponsableId, controlGeneracionId, clasificacionResiduoId, residuoId);
        var result = await reportesService.GenerarAsync(UsuarioId, filtro, cancellationToken);
        return FromResult(result);
    }

    [HttpGet("excel")]
    public async Task<IActionResult> ExportarExcel(
        [FromQuery] DateTime desde,
        [FromQuery] DateTime hasta,
        [FromQuery] Guid? sedeId,
        [FromQuery] Guid? empresaResponsableId,
        [FromQuery] Guid? controlGeneracionId,
        [FromQuery] int? clasificacionResiduoId,
        [FromQuery] Guid? residuoId,
        CancellationToken cancellationToken)
    {
        var filtro = new ReporteResiduosFiltroRequest(
            desde, hasta, sedeId, empresaResponsableId, controlGeneracionId, clasificacionResiduoId, residuoId);
        var result = await reportesService.ExportarExcelAsync(UsuarioId, filtro, cancellationToken);

        if (!result.Succeeded || result.Value is null)
            return StatusCode(result.StatusCode, new { error = result.Error });

        var fileName = $"reporte_residuos_{filtro.Desde:yyyyMMdd}_{filtro.Hasta:yyyyMMdd}.xlsx";
        return File(
            result.Value,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    [HttpGet("archivos")]
    public async Task<IActionResult> ListarArchivos(CancellationToken cancellationToken)
    {
        var result = await reportesService.ListarArchivosAsync(UsuarioId, cancellationToken);
        return FromResult(result);
    }

    [HttpGet("archivos/{archivoId:guid}")]
    public async Task<IActionResult> DescargarArchivo(
        Guid archivoId,
        CancellationToken cancellationToken)
    {
        var result = await reportesService.ObtenerArchivoAsync(archivoId, UsuarioId, cancellationToken);
        if (!result.Succeeded || result.Value is null)
            return StatusCode(result.StatusCode, new { error = result.Error });

        return File(
            result.Value.Contenido,
            result.Value.ContentType,
            result.Value.NombreArchivo);
    }
}
