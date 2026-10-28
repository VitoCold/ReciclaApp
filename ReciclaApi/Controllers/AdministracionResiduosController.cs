using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recicla.Shared.Contracts;
using ReciclaApi.Application.Services;

namespace ReciclaApi.Controllers;

[Authorize(Roles = "ADMINISTRADOR")]
[Route("api/administracion/residuos")]
public sealed class AdministracionResiduosController(
    IAdministracionResiduoService service,
    ILogger<AdministracionResiduosController> logger) : BaseController<AdministracionResiduosController>(logger)
{
    [HttpGet("tipos")]
    public async Task<IActionResult> ListarTipos(CancellationToken cancellationToken) =>
        Ok(await service.ListarTiposAsync(cancellationToken));

    [HttpGet("tipos/{id:guid}")]
    public async Task<IActionResult> ObtenerTipo(Guid id, CancellationToken cancellationToken) =>
        FromResult(await service.ObtenerTipoAsync(id, cancellationToken));

    [HttpPost("tipos")]
    public async Task<IActionResult> CrearTipo([FromBody] GuardarAdministracionTipoResiduoRequest request, CancellationToken cancellationToken) =>
        FromResult(await service.CrearTipoAsync(UsuarioId, request, cancellationToken));

    [HttpPut("tipos/{id:guid}")]
    public async Task<IActionResult> ActualizarTipo(Guid id, [FromBody] GuardarAdministracionTipoResiduoRequest request, CancellationToken cancellationToken) =>
        FromResult(await service.ActualizarTipoAsync(id, UsuarioId, request, cancellationToken));

    [HttpPatch("tipos/{id:guid}/estado")]
    public async Task<IActionResult> CambiarEstadoTipo(Guid id, [FromBody] CambiarEstadoAdministracionTipoResiduoRequest request, CancellationToken cancellationToken) =>
        FromResult(await service.CambiarEstadoTipoAsync(id, UsuarioId, request.EsActivo, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> ListarResiduos(CancellationToken cancellationToken) =>
        Ok(await service.ListarResiduosAsync(cancellationToken));

    [HttpGet("catalogos")]
    public async Task<IActionResult> Catalogos(CancellationToken cancellationToken) =>
        Ok(await service.ObtenerCatalogosResiduosAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObtenerResiduo(Guid id, CancellationToken cancellationToken) =>
        FromResult(await service.ObtenerResiduoAsync(id, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> CrearResiduo([FromBody] GuardarAdministracionResiduoCatalogoRequest request, CancellationToken cancellationToken) =>
        FromResult(await service.CrearResiduoAsync(UsuarioId, request, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> ActualizarResiduo(Guid id, [FromBody] GuardarAdministracionResiduoCatalogoRequest request, CancellationToken cancellationToken) =>
        FromResult(await service.ActualizarResiduoAsync(id, UsuarioId, request, cancellationToken));

    [HttpPatch("{id:guid}/estado")]
    public async Task<IActionResult> CambiarEstadoResiduo(Guid id, [FromBody] CambiarEstadoAdministracionResiduoCatalogoRequest request, CancellationToken cancellationToken) =>
        FromResult(await service.CambiarEstadoResiduoAsync(id, UsuarioId, request.EsActivo, cancellationToken));
}
