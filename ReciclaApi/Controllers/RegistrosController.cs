using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recicla.Shared.Contracts;
using ReciclaApi.Application.Services;

namespace ReciclaApi.Controllers;

[Authorize]
[Route("api/registros")]
public sealed class RegistrosController(
    IRegistroService registroService,
    ILogger<RegistrosController> logger) : BaseController<RegistrosController>(logger)
{
    private const long MaxUploadBytes = 15 * 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        var result = await registroService.ListarAsync(UsuarioId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var result = await registroService.ObtenerAsync(id, UsuarioId, cancellationToken);
        return FromResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Crear(
        [FromBody] CrearRegistroRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogInformation("Creando registro para {UsuarioId}", UsuarioId);
        var result = await registroService.CrearAsync(UsuarioId, request, cancellationToken);
        return FromResult(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(
        Guid id,
        [FromBody] ActualizarRegistroRequest request,
        CancellationToken cancellationToken)
    {
        var result = await registroService.ActualizarAsync(id, UsuarioId, request, cancellationToken);
        return FromResult(result);
    }

    [HttpPost("{id:guid}/residuos")]
    public async Task<IActionResult> AgregarResiduo(
        Guid id,
        [FromBody] AgregarRegistroResiduoRequest request,
        CancellationToken cancellationToken)
    {
        var result = await registroService.AgregarResiduoAsync(id, UsuarioId, request, cancellationToken);
        return FromResult(result);
    }

    [HttpPut("{id:guid}/residuos/{registroResiduoId:guid}")]
    public async Task<IActionResult> ActualizarResiduo(
        Guid id,
        Guid registroResiduoId,
        [FromBody] ActualizarRegistroResiduoRequest request,
        CancellationToken cancellationToken)
    {
        var result = await registroService.ActualizarResiduoAsync(
            id,
            registroResiduoId,
            UsuarioId,
            request,
            cancellationToken);
        return FromResult(result);
    }

    [HttpDelete("{id:guid}/residuos/{registroResiduoId:guid}")]
    public async Task<IActionResult> EliminarResiduo(
        Guid id,
        Guid registroResiduoId,
        CancellationToken cancellationToken)
    {
        var result = await registroService.EliminarResiduoAsync(id, registroResiduoId, UsuarioId, cancellationToken);
        return FromResult(result);
    }

    [HttpPost("{id:guid}/residuos/{registroResiduoId:guid}/fotos")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubirFoto(
        Guid id,
        Guid registroResiduoId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "El archivo está vacío.");

        if (file.Length > MaxUploadBytes)
            return Problem(statusCode: StatusCodes.Status413PayloadTooLarge, detail: "El archivo supera el límite de 15 MB.");

        await using var stream = file.OpenReadStream();
        var result = await registroService.SubirFotoAsync(
            id,
            registroResiduoId,
            UsuarioId,
            stream,
            file.FileName,
            file.ContentType,
            cancellationToken);

        return FromResult(result);
    }

    [HttpPost("{id:guid}/completar")]
    public async Task<IActionResult> Completar(Guid id, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Completando registro {RegistroId} por {UsuarioId}", id, UsuarioId);
        var result = await registroService.CompletarAsync(id, UsuarioId, cancellationToken);
        return FromResult(result);
    }

    [HttpPost("{id:guid}/disposicion")]
    public async Task<IActionResult> GuardarDisposicion(
        Guid id,
        [FromBody] CrearDisposicionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await registroService.GuardarDisposicionAsync(id, UsuarioId, request, cancellationToken);
        return FromResult(result);
    }
}
