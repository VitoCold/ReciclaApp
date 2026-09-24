using Microsoft.AspNetCore.Diagnostics;

namespace ReciclaApi.Middleware;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Error no controlado procesando {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        var statusCode = exception switch
        {
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError
        };

        await Results.Problem(
            statusCode: statusCode,
            title: statusCode == 500 ? "Error interno del servidor" : "No autorizado",
            detail: statusCode == 500 ? "Ocurrió un error inesperado." : exception.Message)
            .ExecuteAsync(httpContext);

        return true;
    }
}
