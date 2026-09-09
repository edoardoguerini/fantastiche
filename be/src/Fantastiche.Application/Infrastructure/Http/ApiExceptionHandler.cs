using Fantastiche.Core.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Fantastiche.Application.Infrastructure.Http;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var error = MapExpectedError(exception);
        if (error is not null)
        {
            await ApiResults.WriteErrorAsync(
                httpContext,
                error.Value.StatusCode,
                error.Value.Code,
                error.Value.Message,
                cancellationToken);
            return true;
        }

        logger.LogError(exception, "Errore HTTP non gestito. CorrelationId: {CorrelationId}", httpContext.TraceIdentifier);
        await ApiResults.WriteErrorAsync(
            httpContext,
            StatusCodes.Status500InternalServerError,
            "server.error",
            "Si è verificato un errore inatteso.",
            cancellationToken);
        return true;
    }

    private static (int StatusCode, string Code, string Message)? MapExpectedError(Exception exception)
    {
        if (exception is DomainException domain)
        {
            return (domain.StatusCode, domain.Code, domain.Message);
        }

        if (exception is ValidationException validation)
        {
            var first = validation.Errors.FirstOrDefault();
            return (
                StatusCodes.Status400BadRequest,
                string.IsNullOrWhiteSpace(first?.ErrorCode) ? "validation.invalid" : first.ErrorCode,
                first?.ErrorMessage ?? "Dati non validi.");
        }

        if (exception is BadHttpRequestException badRequest)
        {
            return (badRequest.StatusCode, "validation.invalid_json", "Richiesta JSON non valida.");
        }

        var sql = exception as SqlException
            ?? (exception as DbUpdateException)?.InnerException as SqlException;
        if (sql?.Number is 2601 or 2627 or 1205 or 51000)
        {
            return (
                StatusCodes.Status409Conflict,
                "conflict.concurrent",
                "La risorsa è stata modificata o esiste già.");
        }

        return null;
    }
}
