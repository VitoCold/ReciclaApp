using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recicla.Shared.Contracts;
using ReciclaApi.Application.Services;

namespace ReciclaApi.Controllers;

[Authorize(Roles = "ADMINISTRADOR")]
[Route("api/administracion")]
public sealed class AdministracionProyectosActividadesController(
    IAdministracionProyectoActividadService service,
    ILogger<AdministracionProyectosActividadesController> logger) : BaseController<AdministracionProyectosActividadesController>(logger)
{
    [HttpGet("proyectos")]
    public async Task<IActionResult> ListarProyectos(CancellationToken cancellationToken) =>
        Ok(await service.ListarProyectosAsync(cancellationToken));

    [HttpGet("proyectos/{id:guid}")]
    public async Task<IActionResult> ObtenerProyecto(Guid id, CancellationToken cancellationToken) =>
        FromResult(await service.ObtenerProyectoAsync(id, cancellationToken));

    [HttpPost("proyectos")]
    public async Task<IActionResult> CrearProyecto([FromBody] GuardarAdministracionProyectoRequest request, CancellationToken cancellationToken) =>
        FromResult(await service.CrearProyectoAsync(UsuarioId, request, cancellationToken));

    [HttpPut("proyectos/{id:guid}")]
    public async Task<IActionResult> ActualizarProyecto(Guid id, [FromBody] GuardarAdministracionProyectoRequest request, CancellationToken cancellationToken) =>
        FromResult(await service.ActualizarProyectoAsync(id, UsuarioId, request, cancellationToken));

    [HttpPatch("proyectos/{id:guid}/estado")]
    public async Task<IActionResult> CambiarEstadoProyecto(Guid id, [FromBody] CambiarEstadoAdministracionProyectoRequest request, CancellationToken cancellationToken) =>
        FromResult(await service.CambiarEstadoProyectoAsync(id, UsuarioId, request.EsActivo, cancellationToken));

    [HttpGet("actividades")]
    public async Task<IActionResult> ListarActividades(CancellationToken cancellationToken) =>
        Ok(await service.ListarActividadesAsync(cancellationToken));

    [HttpGet("actividades/catalogos")]
    public async Task<IActionResult> CatalogosActividades(CancellationToken cancellationToken) =>
        Ok(await service.ObtenerCatalogosActividadesAsync(cancellationToken));

    [HttpGet("actividades/{id:guid}")]
    public async Task<IActionResult> ObtenerActividad(Guid id, CancellationToken cancellationToken) =>
        FromResult(await service.ObtenerActividadAsync(id, cancellationToken));

    [HttpPost("actividades")]
    public async Task<IActionResult> CrearActividad([FromBody] GuardarAdministracionActividadRequest request, CancellationToken cancellationToken) =>
        FromResult(await service.CrearActividadAsync(UsuarioId, request, cancellationToken));

    [HttpPut("actividades/{id:guid}")]
    public async Task<IActionResult> ActualizarActividad(Guid id, [FromBody] GuardarAdministracionActividadRequest request, CancellationToken cancellationToken) =>
        FromResult(await service.ActualizarActividadAsync(id, UsuarioId, request, cancellationToken));

    [HttpPatch("actividades/{id:guid}/estado")]
    public async Task<IActionResult> CambiarEstadoActividad(Guid id, [FromBody] CambiarEstadoAdministracionActividadRequest request, CancellationToken cancellationToken) =>
        FromResult(await service.CambiarEstadoActividadAsync(id, UsuarioId, request.EsActivo, cancellationToken));
}
