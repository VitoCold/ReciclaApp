using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReciclaApi.Application.Services;

namespace ReciclaApi.Controllers;

[Authorize]
[Route("api/catalogos")]
public sealed class CatalogosController(
    ICatalogoService catalogoService,
    ILogger<CatalogosController> logger) : BaseController<CatalogosController>(logger)
{
    [HttpGet("inicial")]
    public async Task<IActionResult> Inicial(CancellationToken cancellationToken)
    {
        Logger.LogDebug("Obteniendo catálogos iniciales para {UsuarioId}", UsuarioId);
        var result = await catalogoService.ObtenerInicialAsync(cancellationToken);
        return Ok(result);
    }
}
