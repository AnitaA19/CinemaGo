using CinemaGo.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace CinemaGo.Web.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            NotFoundException => StatusCodes.Status404NotFound,
            ConflictException => StatusCodes.Status409Conflict,
            CinemaGo.Domain.Exceptions.ValidationException or FluentValidation.ValidationException => StatusCodes.Status400BadRequest,
            ForbiddenException => StatusCodes.Status403Forbidden,
            NotificationDeliveryException => StatusCodes.Status503ServiceUnavailable,
            Microsoft.EntityFrameworkCore.DbUpdateException => StatusCodes.Status409Conflict,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception");
        }
        else
        {
            _logger.LogWarning("Request failed with {StatusCode}: {Message}", status, exception.Message);
        }

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";

        var detail = exception switch
        {
            Microsoft.EntityFrameworkCore.DbUpdateException =>
                "The request conflicts with existing data (e.g. the seat was just booked by someone else).",
            NotificationDeliveryException => exception.Message,
            _ when status == StatusCodes.Status500InternalServerError =>
                "An unexpected error occurred.",
            _ => exception.Message
        };

        var problem = new ProblemDetails
        {
            Type = "about:blank",
            Title = ReasonPhrases.GetReasonPhrase(status),
            Status = status,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        if (exception is FluentValidation.ValidationException validationException)
        {
            problem.Extensions["errors"] = validationException.Errors
                .GroupBy(error => string.IsNullOrWhiteSpace(error.PropertyName) ? "general" : error.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).Distinct().ToArray());
        }
        else if (exception is CinemaGo.Domain.Exceptions.ValidationException)
        {
            problem.Extensions["errors"] = new Dictionary<string, string[]> { ["general"] = [detail] };
        }

        await System.Text.Json.JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            problem,
            cancellationToken: cancellationToken);

        return true;
    }
}
