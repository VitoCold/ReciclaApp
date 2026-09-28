using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recicla.Shared.Contracts;
using ReciclaApi.Application.Services;

namespace ReciclaApi.Controllers;

[Authorize]
[Route("api/controles-generacion")]
public sealed class ControlesGeneracionController(
    IControlGeneracionService controlService,
    IControlGeneracionRegistroConsultaService registroConsultaService,
    ILogger<ControlesGeneracionController> logger) : BaseController<ControlesGeneracionController>(logger)
{
    [HttpGet]
    [Authorize(Roles = "ADMINISTRADOR,AMBIENTAL,RESPONSABLE_OPERATIVO,REGISTRADOR")]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        var result = await controlService.ListarAsync(UsuarioId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "ADMINISTRADOR,AMBIENTAL,RESPONSABLE_OPERATIVO,REGISTRADOR")]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var result = await controlService.ObtenerAsync(id, UsuarioId, cancellationToken);
        return FromResult(result);
    }

    [HttpPost]
    [Authorize(Roles = "RESPONSABLE_OPERATIVO")]
    public async Task<IActionResult> Crear(
        [FromBody] CrearControlGeneracionRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogInformation("Creando control de generación por {UsuarioId}", UsuarioId);
        var result = await controlService.CrearAsync(UsuarioId, request, cancellationToken);
        return FromResult(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "AMBIENTAL")]
    public async Task<IActionResult> ActualizarCabecera(
        Guid id,
        [FromBody] ActualizarControlGeneracionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await controlService.ActualizarCabeceraAsync(id, UsuarioId, request, cancellationToken);
        return FromResult(result);
    }

    [HttpPost("{id:guid}/aprobar")]
    [Authorize(Roles = "AMBIENTAL")]
    public async Task<IActionResult> Aprobar(Guid id, CancellationToken cancellationToken)
    {
        var result = await controlService.AprobarAsync(id, UsuarioId, cancellationToken);
        return FromResult(result);
    }

    [HttpPost("{id:guid}/rechazar")]
    [Authorize(Roles = "AMBIENTAL")]
    public async Task<IActionResult> Rechazar(
        Guid id,
        [FromBody] RechazarControlGeneracionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await controlService.RechazarAsync(id, UsuarioId, request, cancellationToken);
        return FromResult(result);
    }

    [HttpPost("{id:guid}/suspender")]
    [Authorize(Roles = "AMBIENTAL")]
    public Task<IActionResult> Suspender(Guid id, [FromBody] CambiarEstadoControlGeneracionRequest request, CancellationToken cancellationToken) =>
        CambiarEstado(id, "SUSPENDIDO", request, cancellationToken);

    [HttpPost("{id:guid}/reactivar")]
    [Authorize(Roles = "AMBIENTAL")]
    public Task<IActionResult> Reactivar(Guid id, [FromBody] CambiarEstadoControlGeneracionRequest request, CancellationToken cancellationToken) =>
        CambiarEstado(id, "ACTIVO", request, cancellationToken);

    [HttpPost("{id:guid}/cerrar")]
    [Authorize(Roles = "AMBIENTAL")]
    public Task<IActionResult> Cerrar(Guid id, [FromBody] CambiarEstadoControlGeneracionRequest request, CancellationToken cancellationToken) =>
        CambiarEstado(id, "CERRADO", request, cancellationToken);

    [HttpPost("{id:guid}/anular")]
    [Authorize(Roles = "AMBIENTAL")]
    public Task<IActionResult> Anular(Guid id, [FromBody] CambiarEstadoControlGeneracionRequest request, CancellationToken cancellationToken) =>
        CambiarEstado(id, "ANULADO", request, cancellationToken);

    [HttpPost("{id:guid}/usuarios")]
    [Authorize(Roles = "RESPONSABLE_OPERATIVO")]
    public async Task<IActionResult> AsignarUsuario(
        Guid id,
        [FromBody] AsignarControlGeneracionUsuarioRequest request,
        CancellationToken cancellationToken)
    {
        var result = await controlService.AsignarUsuarioAsync(id, UsuarioId, request, cancellationToken);
        return FromResult(result);
    }

    [HttpDelete("{id:guid}/usuarios/{asignacionId:guid}")]
    [Authorize(Roles = "RESPONSABLE_OPERATIVO")]
    public async Task<IActionResult> DesasignarUsuario(
        Guid id,
        Guid asignacionId,
        CancellationToken cancellationToken)
    {
        var result = await controlService.DesasignarUsuarioAsync(id, asignacionId, UsuarioId, cancellationToken);
        return FromResult(result);
    }

    [HttpGet("{id:guid}/registros")]
    [Authorize(Roles = "ADMINISTRADOR,AMBIENTAL,RESPONSABLE_OPERATIVO,REGISTRADOR")]
    public async Task<IActionResult> ListarRegistros(Guid id, CancellationToken cancellationToken)
    {
        var result = await controlService.ListarRegistrosAsync(id, UsuarioId, cancellationToken);
        return FromResult(result);
    }

    [HttpGet("{id:guid}/registros/{registroId:guid}")]
    [Authorize(Roles = "ADMINISTRADOR,AMBIENTAL,RESPONSABLE_OPERATIVO,REGISTRADOR")]
    public async Task<IActionResult> ObtenerRegistro(
        Guid id,
        Guid registroId,
        CancellationToken cancellationToken)
    {
        var result = await registroConsultaService.ObtenerAsync(id, registroId, UsuarioId, cancellationToken);
        return FromResult(result);
    }

    [HttpPost("{id:guid}/registros")]
    [Authorize(Roles = "RESPONSABLE_OPERATIVO,REGISTRADOR")]
    public async Task<IActionResult> CrearRegistro(
        Guid id,
        [FromBody] CrearRegistroEnControlRequest request,
        CancellationToken cancellationToken)
    {
        var result = await controlService.CrearRegistroAsync(id, UsuarioId, request, cancellationToken);
        return FromResult(result);
    }

    [HttpPost("{id:guid}/registros/{registroId:guid}/completar")]
    [Authorize(Roles = "RESPONSABLE_OPERATIVO,REGISTRADOR")]
    public async Task<IActionResult> CompletarRegistro(
        Guid id,
        Guid registroId,
        CancellationToken cancellationToken)
    {
        var result = await registroConsultaService.CompletarAsync(id, registroId, UsuarioId, cancellationToken);
        return FromResult(result);
    }

    private async Task<IActionResult> CambiarEstado(
        Guid id,
        string estado,
        CambiarEstadoControlGeneracionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await controlService.CambiarEstadoAsync(id, UsuarioId, estado, request, cancellationToken);
        return FromResult(result);
    }
}
