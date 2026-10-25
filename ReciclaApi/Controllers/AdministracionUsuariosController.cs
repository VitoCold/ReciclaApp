using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recicla.Shared.Contracts;
using ReciclaApi.Application.Services;

namespace ReciclaApi.Controllers;

[Authorize(Roles = "ADMINISTRADOR")]
[Route("api/administracion/usuarios")]
public sealed class AdministracionUsuariosController(
    IAdministracionUsuarioService service,
    ILogger<AdministracionUsuariosController> logger) : BaseController<AdministracionUsuariosController>(logger)
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
        [FromBody] GuardarAdministracionUsuarioRequest request,
        CancellationToken cancellationToken) =>
        FromResult(await service.CrearAsync(UsuarioId, request, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(
        Guid id,
        [FromBody] GuardarAdministracionUsuarioRequest request,
        CancellationToken cancellationToken) =>
        FromResult(await service.ActualizarAsync(id, UsuarioId, request, cancellationToken));

    [HttpPatch("{id:guid}/estado")]
    public async Task<IActionResult> CambiarEstado(
        Guid id,
        [FromBody] CambiarEstadoAdministracionUsuarioRequest request,
        CancellationToken cancellationToken) =>
        FromResult(await service.CambiarEstadoAsync(id, UsuarioId, request.EsActivo, cancellationToken));

    [HttpPost("{id:guid}/restablecer-clave")]
    public async Task<IActionResult> RestablecerClave(
        Guid id,
        [FromBody] RestablecerClaveAdministracionUsuarioRequest request,
        CancellationToken cancellationToken) =>
        FromResult(await service.RestablecerClaveAsync(id, UsuarioId, request.Password, cancellationToken));
}
