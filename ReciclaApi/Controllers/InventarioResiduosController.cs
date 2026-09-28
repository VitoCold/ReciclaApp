using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recicla.Shared.Contracts;
using ReciclaApi.Application.Services;

namespace ReciclaApi.Controllers;

[Authorize]
[Route("api/inventario-residuos")]
public sealed class InventarioResiduosController(
    IInventarioResiduoService inventarioService,
    ILogger<InventarioResiduosController> logger) : BaseController<InventarioResiduosController>(logger)
{
    [HttpGet]
    [Authorize(Roles = "ADMINISTRADOR,AMBIENTAL,RESPONSABLE_OPERATIVO,REGISTRADOR")]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        var result = await inventarioService.ListarAsync(UsuarioId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{registroResiduoId:guid}/almacenar")]
    [Authorize(Roles = "AMBIENTAL,RESPONSABLE_OPERATIVO")]
    public async Task<IActionResult> Almacenar(
        Guid registroResiduoId,
        [FromBody] AlmacenarResiduoRequest request,
        CancellationToken cancellationToken)
    {
        var result = await inventarioService.AlmacenarAsync(registroResiduoId, UsuarioId, request, cancellationToken);
        return FromResult(result);
    }
}
