using CinemaGo.Domain.Exceptions;
using CinemaGo.Web.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CinemaGo.Tests;

public class GlobalExceptionHandlerTests
{
    private static async Task<int> HandleAsync(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var handled = await new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance)
            .TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        return context.Response.StatusCode;
    }

    [Fact]
    public async Task NotFoundException_Returns404() =>
        Assert.Equal(404, await HandleAsync(new NotFoundException("missing")));

    [Fact]
    public async Task ConflictException_Returns409() =>
        Assert.Equal(409, await HandleAsync(new ConflictException("taken")));

    [Fact]
    public async Task DbUpdateException_Returns409() =>
        Assert.Equal(409, await HandleAsync(new DbUpdateException("unique index")));

    [Fact]
    public async Task ValidationException_Returns400() =>
        Assert.Equal(400, await HandleAsync(new ValidationException("bad")));
}
