using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recicla.Shared.Contracts;
using ReciclaApi.Application.Services;

namespace ReciclaApi.Controllers;

[Authorize]
[Route("api/retiros")]
public sealed class RetirosController(
    IRetiroService retiroService,
    ILogger<RetirosController> logger) : BaseController<RetirosController>(logger)
{
    [HttpGet]
    [Authorize(Roles = "ADMINISTRADOR,AMBIENTAL,RESPONSABLE_OPERATIVO")]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        var retiros = await retiroService.ListarAsync(UsuarioId, cancellationToken);
        return Ok(retiros);
    }

    [HttpGet("{retiroId:guid}")]
    [Authorize(Roles = "ADMINISTRADOR,AMBIENTAL,RESPONSABLE_OPERATIVO")]
    public async Task<IActionResult> Obtener(Guid retiroId, CancellationToken cancellationToken)
    {
        var result = await retiroService.ObtenerAsync(retiroId, UsuarioId, cancellationToken);
        return FromResult(result);
    }

    [HttpPost]
    [Authorize(Roles = "AMBIENTAL,RESPONSABLE_OPERATIVO")]
    public async Task<IActionResult> Crear(
        [FromBody] CrearRetiroRequest request,
        CancellationToken cancellationToken)
    {
        var result = await retiroService.CrearAsync(UsuarioId, request, cancellationToken);
        return FromResult(result);
    }
}
