using System.Text.Json;
using CinemaGo.Web.Responses;
using Microsoft.AspNetCore.WebUtilities;

namespace CinemaGo.Web.Middleware;

public sealed class ApiResponseMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RequestDelegate _next;

    public ApiResponseMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        var originalBody = context.Response.Body;
        await using var responseBuffer = new MemoryStream();
        context.Response.Body = responseBuffer;

        try
        {
            await _next(context);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        responseBuffer.Position = 0;

        if (context.Response.StatusCode is StatusCodes.Status204NoContent
            or StatusCodes.Status205ResetContent
            or StatusCodes.Status304NotModified)
        {
            await responseBuffer.CopyToAsync(originalBody, context.RequestAborted);
            return;
        }

        if (context.Response.StatusCode >= StatusCodes.Status400BadRequest
            && responseBuffer.Length == 0)
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength = null;
            await JsonSerializer.SerializeAsync(
                originalBody,
                new ApiResponse<object>
                {
                    Success = false,
                    Message = ReasonPhrases.GetReasonPhrase(context.Response.StatusCode)
                },
                JsonOptions,
                context.RequestAborted);
            return;
        }

        var isJson = context.Response.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true;
        if (context.Response.StatusCode is >= StatusCodes.Status200OK and < StatusCodes.Status300MultipleChoices
            && isJson)
        {
            try
            {
                using var document = await JsonDocument.ParseAsync(
                    responseBuffer,
                    cancellationToken: context.RequestAborted);

                if (!IsApiResponse(document.RootElement))
                {
                    context.Response.ContentType = "application/json; charset=utf-8";
                    context.Response.ContentLength = null;
                    await JsonSerializer.SerializeAsync(
                        originalBody,
                        new ApiResponse<JsonElement>
                        {
                            Success = true,
                            Message = "Request completed successfully.",
                            Data = document.RootElement
                        },
                        JsonOptions,
                        context.RequestAborted);
                    return;
                }
            }
            catch (JsonException)
            {
                responseBuffer.Position = 0;
                await responseBuffer.CopyToAsync(originalBody, context.RequestAborted);
                return;
            }
        }

        responseBuffer.Position = 0;
        await responseBuffer.CopyToAsync(originalBody, context.RequestAborted);
    }

    private static bool IsApiResponse(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty("success", out _)
        && element.TryGetProperty("message", out _)
        && element.TryGetProperty("data", out _)
        && element.TryGetProperty("errors", out _);
}
