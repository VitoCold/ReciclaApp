using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ReciclaApi.Application.Common;

namespace ReciclaApi.Controllers;

[ApiController]
public abstract class BaseController<TController>(ILogger<TController> logger) : ControllerBase
{
    protected ILogger<TController> Logger { get; } = logger;

    protected Guid UsuarioId
    {
        get
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(value, out var usuarioId))
                return usuarioId;

            throw new UnauthorizedAccessException("No se encontró el identificador del usuario autenticado.");
        }
    }

    protected IActionResult FromResult<T>(ServiceResult<T> result) =>
        result.Succeeded
            ? StatusCode(result.StatusCode, result.Value)
            : Problem(statusCode: result.StatusCode, detail: result.Error);

    protected IActionResult FromResult(ServiceResult result) =>
        result.Succeeded
            ? StatusCode(result.StatusCode)
            : Problem(statusCode: result.StatusCode, detail: result.Error);
}
