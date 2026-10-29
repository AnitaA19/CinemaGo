using System.Text.Json;
using CinemaGo.Web.Middleware;
using Microsoft.AspNetCore.Http;

namespace CinemaGo.Tests;

public class ApiResponseMiddlewareTests
{
    [Fact]
    public async Task SuccessfulJsonResponse_IsWrappedInApiResponse()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/movies";
        context.Response.Body = new MemoryStream();

        var middleware = new ApiResponseMiddleware(async httpContext =>
        {
            httpContext.Response.ContentType = "application/json";
            await httpContext.Response.WriteAsJsonAsync(new { id = 7 });
        });

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var response = await JsonDocument.ParseAsync(context.Response.Body);
        var root = response.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(7, root.GetProperty("data").GetProperty("id").GetInt32());
        Assert.Equal("Request completed successfully.", root.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("errors").ValueKind);
    }

    [Fact]
    public async Task EmptyErrorResponse_IsReturnedAsApiResponse()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/movies/42";
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.Body = new MemoryStream();

        var middleware = new ApiResponseMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var response = await JsonDocument.ParseAsync(context.Response.Body);
        var root = response.RootElement;

        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("Not Found", root.GetProperty("message").GetString());
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task NoContentResponse_RemainsEmpty()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/movies/42";
        context.Response.StatusCode = StatusCodes.Status204NoContent;
        context.Response.Body = new MemoryStream();

        var middleware = new ApiResponseMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal(0, context.Response.Body.Length);
    }
}
