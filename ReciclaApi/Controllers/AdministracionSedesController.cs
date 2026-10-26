using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recicla.Shared.Contracts;
using ReciclaApi.Application.Services;

namespace ReciclaApi.Controllers;

[Authorize(Roles = "ADMINISTRADOR")]
[Route("api/administracion/sedes")]
public sealed class AdministracionSedesController(
    IAdministracionSedeService service,
    ILogger<AdministracionSedesController> logger) : BaseController<AdministracionSedesController>(logger)
{
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken) =>
        Ok(await service.ListarAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancellationToken) =>
        FromResult(await service.ObtenerAsync(id, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Crear(
        [FromBody] GuardarAdministracionSedeRequest request,
        CancellationToken cancellationToken) =>
        FromResult(await service.CrearAsync(UsuarioId, request, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(
        Guid id,
        [FromBody] GuardarAdministracionSedeRequest request,
        CancellationToken cancellationToken) =>
        FromResult(await service.ActualizarAsync(id, UsuarioId, request, cancellationToken));

    [HttpPatch("{id:guid}/estado")]
    public async Task<IActionResult> CambiarEstado(
        Guid id,
        [FromBody] CambiarEstadoAdministracionSedeRequest request,
        CancellationToken cancellationToken) =>
        FromResult(await service.CambiarEstadoAsync(id, UsuarioId, request.EsActivo, cancellationToken));
}
