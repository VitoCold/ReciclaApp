using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recicla.Shared.Contracts;
using ReciclaApi.Application.Services;

namespace ReciclaApi.Controllers;

[Authorize]
[Route("api/disposiciones-finales")]
public sealed class DisposicionesFinalesController(
    IDisposicionFinalService disposicionService,
    ILogger<DisposicionesFinalesController> logger) : BaseController<DisposicionesFinalesController>(logger)
{
    private const long MaxUploadBytes = 15 * 1024 * 1024;

    [HttpGet("tipos-tratamiento")]
    public async Task<IActionResult> ListarTiposTratamiento(CancellationToken cancellationToken) =>
        Ok(await disposicionService.ListarTiposTratamientoAsync(cancellationToken));

    [HttpGet("retiro/{retiroId:guid}")]
    public async Task<IActionResult> ObtenerPorRetiro(Guid retiroId, CancellationToken cancellationToken)
    {
        var result = await disposicionService.ObtenerPorRetiroAsync(retiroId, UsuarioId, cancellationToken);
        return FromResult(result);
    }

    [HttpPost("retiro/{retiroId:guid}")]
    [Authorize(Roles = "AMBIENTAL,RESPONSABLE_OPERATIVO")]
    public async Task<IActionResult> Crear(
        Guid retiroId,
        [FromBody] CrearDisposicionFinalRequest request,
        CancellationToken cancellationToken)
    {
        var result = await disposicionService.CrearAsync(retiroId, UsuarioId, request, cancellationToken);
        return FromResult(result);
    }

    [HttpPost("{id:guid}/evidencias")]
    [Authorize(Roles = "AMBIENTAL,RESPONSABLE_OPERATIVO")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubirEvidencia(
        Guid id,
        IFormFile file,
        [FromForm] string tipoEvidencia,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "El archivo está vacío.");

        if (file.Length > MaxUploadBytes)
            return Problem(statusCode: StatusCodes.Status413PayloadTooLarge, detail: "El archivo supera el límite de 15 MB.");

        await using var stream = file.OpenReadStream();
        var result = await disposicionService.SubirEvidenciaAsync(
            id,
            UsuarioId,
            stream,
            file.FileName,
            file.ContentType,
            tipoEvidencia,
            cancellationToken);

        return FromResult(result);
    }

    [HttpPost("{id:guid}/validar")]
    [Authorize(Roles = "AMBIENTAL")]
    public async Task<IActionResult> Validar(Guid id, CancellationToken cancellationToken)
    {
        var result = await disposicionService.ValidarAsync(id, UsuarioId, cancellationToken);
        return FromResult(result);
    }
}
