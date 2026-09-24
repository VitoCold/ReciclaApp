using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recicla.Shared.Contracts;
using ReciclaApi.Application.Services;

namespace ReciclaApi.Controllers;

[Route("api/auth")]
public sealed class AuthController(
    IAuthService authService,
    ILogger<AuthController> logger) : BaseController<AuthController>(logger)
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        Logger.LogInformation("Intento de inicio de sesión para {Usuario}", request.Usuario);
        var result = await authService.LoginAsync(request, cancellationToken);

        if (!result.Succeeded)
            Logger.LogWarning("Inicio de sesión rechazado para {Usuario}", request.Usuario);

        return FromResult(result);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var result = await authService.MeAsync(UsuarioId, cancellationToken);
        return FromResult(result);
    }
}
