using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recicla.Shared.Contracts;
using ReciclaApi.Application.Services;

namespace ReciclaApi.Controllers;

[Authorize]
[Route("api/registros/{registroId:guid}/evidencias-residuos")]
public sealed class RegistroResiduoEvidenciasController(
    IRegistroResiduoEvidenciaService service,
    ILogger<RegistroResiduoEvidenciasController> logger) : BaseController<RegistroResiduoEvidenciasController>(logger)
{
    [HttpGet]
    public async Task<IActionResult> Listar(Guid registroId, CancellationToken cancellationToken)
    {
        var result = await service.ListarAsync(registroId, UsuarioId, cancellationToken);
        return FromResult(result);
    }

    [HttpPut("{registroResiduoId:guid}/ubicacion")]
    public async Task<IActionResult> GuardarUbicacion(
        Guid registroId,
        Guid registroResiduoId,
        [FromBody] GuardarUbicacionResiduoRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogInformation(
            "Guardando ubicación del residuo {RegistroResiduoId} del registro {RegistroId}",
            registroResiduoId,
            registroId);

        var result = await service.GuardarUbicacionAsync(
            registroId,
            registroResiduoId,
            UsuarioId,
            request,
            cancellationToken);
        return FromResult(result);
    }
}
