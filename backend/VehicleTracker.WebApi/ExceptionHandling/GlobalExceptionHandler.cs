using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using VehicleTracker.Application.Common.Validation;

namespace VehicleTracker.WebApi.ExceptionHandling;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var isBadRequest = exception is VehicleValidationException;

        var statusCode = isBadRequest
            ? StatusCodes.Status400BadRequest
            : StatusCodes.Status500InternalServerError;

        if (!isBadRequest)
        {
            _logger.LogError(
                exception,
                "Beklenmeyen API hatasi. Path: {Path}",
                httpContext.Request.Path);
        }

        var detail = isBadRequest
            ? exception.Message
            : "An unexpected server error occurred.";

        if (exception is VehicleValidationException validationException)
        {
            detail = ValidationErrorMessages.GetMessage(
                validationException.Code,
                httpContext.Request.Headers.AcceptLanguage.ToString());
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = isBadRequest
                ? "Bad Request"
                : "Internal Server Error",
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        if (exception is VehicleValidationException validation)
        {
            problem.Extensions["code"] = validation.Code;
        }

        httpContext.Response.StatusCode = statusCode;

        var problemDetailsService = httpContext.RequestServices
            .GetRequiredService<IProblemDetailsService>();

        var written = await problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problem,
                Exception = exception
            });

        if (!written)
        {
            httpContext.Response.ContentType =
                "application/problem+json";

            await httpContext.Response.WriteAsJsonAsync(
                problem,
                cancellationToken);
        }

        return true;
    }
}
