using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReciclaApi.Application.Services;

namespace ReciclaApi.Controllers;

[Authorize]
[Route("api/disposiciones")]
public sealed class DisposicionesController(
    IRegistroService registroService,
    ILogger<DisposicionesController> logger) : BaseController<DisposicionesController>(logger)
{
    private const long MaxUploadBytes = 15 * 1024 * 1024;

    [HttpPost("{id:guid}/evidencias")]
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

        Logger.LogInformation("Subiendo evidencia para disposición {DisposicionId}", id);
        await using var stream = file.OpenReadStream();
        var result = await registroService.SubirEvidenciaAsync(
            id,
            UsuarioId,
            stream,
            file.FileName,
            file.ContentType,
            tipoEvidencia,
            cancellationToken);

        return FromResult(result);
    }
}
