using CinemaGo.Domain.Exceptions;
using CinemaGo.Web.Responses;
using Microsoft.AspNetCore.Diagnostics;

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
            ValidationException => StatusCodes.Status400BadRequest,
            ForbiddenException => StatusCodes.Status403Forbidden,
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

        var message = exception is Microsoft.EntityFrameworkCore.DbUpdateException
            ? "The request conflicts with existing data (e.g. the seat was just booked by someone else)."
            : status == StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred."
                : exception.Message;

        var response = new ApiResponse<object>
        {
            Success = false,
            Message = message,
            Errors = exception is ValidationException
                ? new Dictionary<string, string[]> { ["general"] = [message] }
                : null
        };

        await httpContext.Response.WriteAsJsonAsync(
            response,
            cancellationToken: cancellationToken);

        return true;
    }
}
