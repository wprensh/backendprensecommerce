using Microsoft.AspNetCore.Diagnostics;

namespace PrensEcommerce.Api.Common.Errors;

/// <summary>Convierte excepciones no controladas en ProblemDetails 500 sin filtrar detalles internos.</summary>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // JSON mal formado o parámetros inválidos: es culpa del cliente, no un 500.
        var (status, title) = exception is BadHttpRequestException badRequest
            ? (badRequest.StatusCode, "Petición no válida")
            : (StatusCodes.Status500InternalServerError, "Error interno");

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Error no controlado procesando {Path}", httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Title = title, Status = status },
        });
    }
}
