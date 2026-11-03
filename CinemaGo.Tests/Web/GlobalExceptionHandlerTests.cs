using CinemaGo.Domain.Exceptions;
using CinemaGo.Web.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CinemaGo.Tests.Web;

public class GlobalExceptionHandlerTests
{
    private static async Task<DefaultHttpContext> HandleAsync(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();

        var handled = await new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance)
            .TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        return context;
    }

    [Fact]
    public async Task NotFoundException_ReturnsProblemDetails()
    {
        var context = await HandleAsync(new NotFoundException("missing"));
        Assert.Equal(404, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);

        context.Response.Body.Position = 0;
        using var document = await System.Text.Json.JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(404, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("missing", document.RootElement.GetProperty("detail").GetString());
        Assert.Equal("/api/test", document.RootElement.GetProperty("instance").GetString());
    }

    [Fact]
    public async Task ConflictException_Returns409() =>
        Assert.Equal(409, (await HandleAsync(new ConflictException("taken"))).Response.StatusCode);

    [Fact]
    public async Task DbUpdateException_Returns409() =>
        Assert.Equal(409, (await HandleAsync(new DbUpdateException("unique index"))).Response.StatusCode);

    [Fact]
    public async Task NotificationDeliveryException_Returns503WithActionableDetail()
    {
        var context = await HandleAsync(new NotificationDeliveryException("SMTP is not configured."));

        Assert.Equal(503, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var document = await System.Text.Json.JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("SMTP is not configured.", document.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task ValidationException_Returns400WithErrorsExtension()
    {
        var context = await HandleAsync(new ValidationException("bad"));
        Assert.Equal(400, context.Response.StatusCode);

        context.Response.Body.Position = 0;
        using var document = await System.Text.Json.JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("bad", document.RootElement.GetProperty("errors").GetProperty("general")[0].GetString());
    }
}
