using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TmsApi.Application.Common;
using DataAnnotationsValidationException = System.ComponentModel.DataAnnotations.ValidationException;

namespace TmsApi.Api.ExceptionHandlers;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken ct
    )
    {
        var (status, title, detail, type, errors) = exception switch
        {
            ValidationException ve => (
                StatusCodes.Status400BadRequest,
                "Validation failed",
                "One or more fields are invalid. See errors for details.",
                "https://tms.local/errors/validation_failed",
                (IDictionary<string, string[]>?)
                    ve
                        .Errors.GroupBy(e => e.PropertyName)
                        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            ),
            DataAnnotationsValidationException validation => (
                StatusCodes.Status400BadRequest,
                "Validation failed",
                validation.Message,
                "https://tms.local/errors/validation_failed",
                null
            ),
            DuplicateRegistrationNumberException duplicate => (
                StatusCodes.Status409Conflict,
                "Duplicate registration number",
                duplicate.Message,
                "https://tms.local/errors/duplicate_registration_number",
                null
            ),
            EnrollmentRejectedException rejected => (
                rejected.Error.Code switch
                {
                    "student_not_found" or "course_not_found" => StatusCodes.Status404NotFound,
                    "already_enrolled" => StatusCodes.Status409Conflict,
                    _ => StatusCodes.Status400BadRequest,
                },
                "Enrollment rejected",
                rejected.Message,
                $"https://tms.local/errors/{rejected.Error.Code}",
                null
            ),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Server error",
                $"An unexpected error occurred. Trace ID: {httpContext.TraceIdentifier}",
                "https://tms.local/errors/server_error",
                null
            ),
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(
                exception,
                "Unhandled exception (trace={TraceId})",
                httpContext.TraceIdentifier
            );

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Type = type,
            Instance = httpContext.Request.Path,
        };

        if (errors is not null)
            problem.Extensions["errors"] = errors;

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problem, ct);

        return true;
    }
}
