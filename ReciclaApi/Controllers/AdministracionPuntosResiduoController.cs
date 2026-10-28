using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recicla.Shared.Contracts;
using ReciclaApi.Application.Services;

namespace ReciclaApi.Controllers;

[Authorize(Roles = "ADMINISTRADOR")]
[Route("api/administracion/puntos-residuo")]
public sealed class AdministracionPuntosResiduoController(
    IAdministracionPuntoResiduoService service,
    ILogger<AdministracionPuntosResiduoController> logger) : BaseController<AdministracionPuntosResiduoController>(logger)
{
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken) =>
        Ok(await service.ListarAsync(cancellationToken));

    [HttpGet("catalogos")]
    public async Task<IActionResult> Catalogos(CancellationToken cancellationToken) =>
        Ok(await service.ObtenerCatalogosAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancellationToken) =>
        FromResult(await service.ObtenerAsync(id, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Crear(
        [FromBody] GuardarAdministracionPuntoResiduoRequest request,
        CancellationToken cancellationToken) =>
        FromResult(await service.CrearAsync(UsuarioId, request, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(
        Guid id,
        [FromBody] GuardarAdministracionPuntoResiduoRequest request,
        CancellationToken cancellationToken) =>
        FromResult(await service.ActualizarAsync(id, UsuarioId, request, cancellationToken));

    [HttpPatch("{id:guid}/estado")]
    public async Task<IActionResult> CambiarEstado(
        Guid id,
        [FromBody] CambiarEstadoAdministracionPuntoResiduoRequest request,
        CancellationToken cancellationToken) =>
        FromResult(await service.CambiarEstadoAsync(id, UsuarioId, request.EsActivo, cancellationToken));
}
