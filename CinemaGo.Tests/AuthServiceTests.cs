using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CinemaGo.Application.Services.Implementations;
using CinemaGo.Domain.DTOs.AuthDTOs;
using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Enums;
using CinemaGo.Domain.Exceptions;
using CinemaGo.Infrastructure.Data;
using CinemaGo.Infrastructure.Repositories;
using CinemaGo.Infrastructure.UnitOfWork;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CinemaGo.Tests;

// Uses the real repositories, password hasher and JWT generation against SQLite.
public sealed class AuthServiceTests : IDisposable
{
    private const string Password = "Str0ng!Password";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly PasswordHasher<User> _hasher = new();

    public AuthServiceTests()
    {
        _connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private CinemaDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<CinemaDbContext>().UseSqlite(_connection).Options);

    private static IConfiguration CreateConfiguration(string? key = "this-is-a-test-signing-key-of-32-bytes!!") =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = key,
            ["Jwt:Issuer"] = "CinemaGo",
            ["Jwt:Audience"] = "CinemaGoUsers",
            ["Jwt:ExpiresInMinutes"] = "30"
        }).Build();

    private AuthService CreateService(CinemaDbContext context, IConfiguration? configuration = null) => new(
        new UserRepository(context),
        new UnitOfWork(context),
        _hasher,
        configuration ?? CreateConfiguration());

    private static RegisterDTO Register(string username = "alice", string email = "alice@example.com") =>
        new() { Username = username, Email = email, Password = Password };

    private static JwtSecurityToken ReadToken(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    [Fact]
    public async Task Register_NewUser_PersistsHashedPasswordAndCustomerRole()
    {
        using var context = CreateContext();

        var result = await CreateService(context).RegisterAsync(Register(), CancellationToken.None);

        Assert.Equal("alice", result.Username);
        Assert.Equal(nameof(UserRole.Customer), result.Role);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));

        using var verify = CreateContext();
        var user = await verify.Users.SingleAsync();
        Assert.NotEqual(Password, user.PasswordHash);
        Assert.Equal(UserRole.Customer, user.Role);
        Assert.Equal(PasswordVerificationResult.Success, _hasher.VerifyHashedPassword(user, user.PasswordHash, Password));
    }

    [Fact]
    public async Task Register_TrimsUsernameAndEmail()
    {
        using var context = CreateContext();

        var result = await CreateService(context).RegisterAsync(
            Register("  alice  ", "  alice@example.com "), CancellationToken.None);

        Assert.Equal("alice", result.Username);
        Assert.Equal("alice@example.com", result.Email);
    }

    [Fact]
    public async Task Register_CannotSelfAssignAdminRole()
    {
        using var context = CreateContext();

        var result = await CreateService(context).RegisterAsync(Register("admin", "admin@example.com"), CancellationToken.None);

        Assert.Equal(nameof(UserRole.Customer), result.Role);
    }

    [Fact]
    public async Task Register_DuplicateUsername_ThrowsConflict()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        await service.RegisterAsync(Register(), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => service.RegisterAsync(Register("alice", "other@example.com"), CancellationToken.None));

        Assert.Contains("Username", ex.Message);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ThrowsConflict()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        await service.RegisterAsync(Register(), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => service.RegisterAsync(Register("bob", "alice@example.com"), CancellationToken.None));

        Assert.Contains("Email", ex.Message);
    }

    [Theory]
    [InlineData("ab", "a@x.com")]
    [InlineData("   ", "a@x.com")]
    [InlineData("alice", "   ")]
    public async Task Register_InvalidInput_ThrowsValidation(string username, string email)
    {
        using var context = CreateContext();

        await Assert.ThrowsAsync<ValidationException>(
            () => CreateService(context).RegisterAsync(Register(username, email), CancellationToken.None));

        Assert.Empty(await context.Users.ToListAsync());
    }

    [Fact]
    public async Task Login_ByUsername_ReturnsTokenWithClaims()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        var registered = await service.RegisterAsync(Register(), CancellationToken.None);

        var result = await service.LoginAsync(
            new LoginDTO { UsernameOrEmail = "alice", Password = Password }, CancellationToken.None);

        Assert.Equal(registered.UserId, result.UserId);
        var token = ReadToken(result.Token);
        Assert.Equal("CinemaGo", token.Issuer);
        Assert.Contains("CinemaGoUsers", token.Audiences);
        Assert.Equal(registered.UserId.ToString(), token.Claims.Single(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "nameid").Value);
        Assert.Equal("Customer", token.Claims.Single(c => c.Type == ClaimTypes.Role || c.Type == "role").Value);
        Assert.True(result.ExpiresAtUtc > DateTime.UtcNow.AddMinutes(29));
        Assert.True(result.ExpiresAtUtc <= DateTime.UtcNow.AddMinutes(30));
    }

    [Fact]
    public async Task Login_ByEmail_Succeeds()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        await service.RegisterAsync(Register(), CancellationToken.None);

        var result = await service.LoginAsync(
            new LoginDTO { UsernameOrEmail = " alice@example.com ", Password = Password }, CancellationToken.None);

        Assert.Equal("alice", result.Username);
    }

    [Fact]
    public async Task Login_WrongPassword_ThrowsUnauthorized()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        await service.RegisterAsync(Register(), CancellationToken.None);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.LoginAsync(
                new LoginDTO { UsernameOrEmail = "alice", Password = "wrong" }, CancellationToken.None));
    }

    [Theory]
    [InlineData("nobody")]
    [InlineData("nobody@example.com")]
    public async Task Login_UnknownUser_ThrowsUnauthorizedWithSameMessageAsWrongPassword(string login)
    {
        using var context = CreateContext();
        var service = CreateService(context);
        await service.RegisterAsync(Register(), CancellationToken.None);

        var unknown = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.LoginAsync(new LoginDTO { UsernameOrEmail = login, Password = Password }, CancellationToken.None));
        var wrong = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.LoginAsync(new LoginDTO { UsernameOrEmail = "alice", Password = "wrong" }, CancellationToken.None));

        // No user enumeration: both failures are indistinguishable.
        Assert.Equal(wrong.Message, unknown.Message);
    }

    [Fact]
    public async Task Login_Admin_TokenContainsAdminRole()
    {
        using var context = CreateContext();
        var admin = new User { Username = "root", Email = "root@example.com", Role = UserRole.Admin };
        admin.PasswordHash = _hasher.HashPassword(admin, Password);
        context.Users.Add(admin);
        await context.SaveChangesAsync();

        var result = await CreateService(context).LoginAsync(
            new LoginDTO { UsernameOrEmail = "root", Password = Password }, CancellationToken.None);

        Assert.Equal("Admin", result.Role);
        Assert.Equal("Admin", ReadToken(result.Token).Claims.Single(c => c.Type == ClaimTypes.Role || c.Type == "role").Value);
    }

    [Fact]
    public async Task Register_ShortSigningKey_ThrowsInvalidOperation()
    {
        using var context = CreateContext();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(context, CreateConfiguration("short")).RegisterAsync(Register(), CancellationToken.None));
    }

    [Fact]
    public async Task Register_MissingSigningKey_ThrowsInvalidOperation()
    {
        using var context = CreateContext();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(context, CreateConfiguration(null)).RegisterAsync(Register(), CancellationToken.None));
    }

    [Fact]
    public async Task Register_ConcurrentSameUsername_UniqueIndexRejectsSecondUser()
    {
        using var first = CreateContext();
        using var second = CreateContext();

        await CreateService(first).RegisterAsync(Register(), CancellationToken.None);

        // The second request already passed the "is the username free" check.
        second.Users.Add(new User { Username = "alice", Email = "dup@example.com", PasswordHash = "h" });
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());

        using var verify = CreateContext();
        Assert.Equal(1, await verify.Users.CountAsync());
    }
}
