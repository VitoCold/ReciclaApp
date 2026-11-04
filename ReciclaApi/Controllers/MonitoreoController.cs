using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recicla.Shared.Contracts;
using ReciclaApi.Application.Services;

namespace ReciclaApi.Controllers;

[Authorize(Roles = "AMBIENTAL,ADMINISTRADOR")]
[Route("api/monitoreo")]
public sealed class MonitoreoController(
    IMonitoreoService service,
    ILogger<MonitoreoController> logger) : BaseController<MonitoreoController>(logger)
{
    [HttpGet("actividad")]
    public async Task<IActionResult> Actividad(
        [FromQuery] DateTime? desdeUtc,
        [FromQuery] DateTime? hastaUtc,
        [FromQuery] Guid? sedeId,
        [FromQuery] int? clasificacionResiduoId,
        [FromQuery] string? estadoOperativo,
        [FromQuery] string? buscar,
        [FromQuery] int limite = 150,
        CancellationToken cancellationToken = default)
    {
        var filtro = new MonitoreoActividadFiltroRequest(
            desdeUtc,
            hastaUtc,
            sedeId,
            clasificacionResiduoId,
            estadoOperativo,
            buscar,
            limite);

        return Ok(await service.ObtenerActividadAsync(filtro, cancellationToken));
    }
}
