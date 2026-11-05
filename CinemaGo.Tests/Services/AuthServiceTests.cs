using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CinemaGo.Application.Services.Implementations;
using CinemaGo.Domain.DTOs.AuthDTOs;
using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Enums;
using CinemaGo.Domain.Exceptions;
using CinemaGo.Domain.Intefraces;
using CinemaGo.Infrastructure.Data;
using CinemaGo.Infrastructure.Repositories;
using CinemaGo.Infrastructure.UnitOfWork;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CinemaGo.Tests.Services;

// Uses the real repositories, password hasher and JWT generation against SQLite.
public sealed class AuthServiceTests
{
    private const string Password = "Str0ng!Password";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly PasswordHasher<User> _hasher = new();
    private readonly CapturingEmailSender _emailSender = new();

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
            ["Jwt:ExpiresInMinutes"] = "30",
            ["AdminInvitation:AcceptUrl"] = "https://cinema.example.com/accept-admin-invite"
        }).Build();

    private AuthService CreateService(
        CinemaDbContext context,
        IConfiguration? configuration = null,
        IAdminInvitationEmailSender? emailSender = null) => new(
        new UserRepository(context),
        new RefreshTokenRepository(context),
        new AdminInvitationRepository(context),
        emailSender ?? _emailSender,
        new UnitOfWork(context),
        _hasher,
        configuration ?? CreateConfiguration());

    private sealed class CapturingEmailSender : IAdminInvitationEmailSender
    {
        public List<(string Recipient, string Code, string Link)> Sent { get; } = [];

        public Task SendAsync(string recipient, string code, string acceptLink, DateTime expiresAtUtc, CancellationToken cancellationToken)
        {
            Sent.Add((recipient, code, acceptLink));
            return Task.CompletedTask;
        }
    }

    private sealed class FailingEmailSender : IAdminInvitationEmailSender
    {
        public Task SendAsync(string recipient, string code, string acceptLink, DateTime expiresAtUtc, CancellationToken cancellationToken) =>
            throw new NotificationDeliveryException("SMTP down.");
    }

    private async Task<User> AddSuperAdminAsync(CinemaDbContext context)
    {
        var superAdmin = new User { Username = "boss", Email = "boss@example.com", Role = UserRole.SuperAdmin };
        superAdmin.PasswordHash = _hasher.HashPassword(superAdmin, Password);
        context.Users.Add(superAdmin);
        await context.SaveChangesAsync();
        return superAdmin;
    }

    private static AcceptAdminInvitationDTO Accept(string code, string email = "new.admin@example.com", string username = "newadmin") =>
        new() { Email = email, Username = username, Password = Password, Code = code };

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
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
        Assert.True(result.RefreshTokenExpiresAtUtc > DateTime.UtcNow.AddDays(6));

        using var verify = CreateContext();
        var user = await verify.Users.SingleAsync();
        var storedRefreshToken = await verify.RefreshTokens.SingleAsync();
        Assert.NotEqual(result.RefreshToken, storedRefreshToken.TokenHash);
        Assert.Equal(64, storedRefreshToken.TokenHash.Length);
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
    public async Task InviteAdmin_SendsCodeAndDoesNotCreateUser()
    {
        using var context = CreateContext();
        var boss = await AddSuperAdminAsync(context);

        var result = await CreateService(context).InviteAdminAsync(
            new InviteAdminDTO { Email = " New.Admin@Example.com " }, boss.Id, CancellationToken.None);

        Assert.Equal("new.admin@example.com", result.Email);
        var sent = Assert.Single(_emailSender.Sent);
        Assert.Equal("new.admin@example.com", sent.Recipient);
        Assert.Matches(@"^\d{8}$", sent.Code);
        Assert.Contains("email=new.admin%40example.com", sent.Link);
        Assert.Equal(1, await context.Users.CountAsync());
        var invitation = await context.AdminInvitations.SingleAsync();
        Assert.NotEqual(sent.Code, invitation.CodeHash);
    }

    [Fact]
    public async Task AcceptInvitation_ValidCode_CreatesAdminAndReturnsTokens()
    {
        using var context = CreateContext();
        var boss = await AddSuperAdminAsync(context);
        var service = CreateService(context);
        await service.InviteAdminAsync(new InviteAdminDTO { Email = "new.admin@example.com" }, boss.Id, CancellationToken.None);

        var result = await service.AcceptAdminInvitationAsync(Accept(_emailSender.Sent[0].Code), CancellationToken.None);

        Assert.Equal("Admin", result.Role);
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));
        var admin = await context.Users.SingleAsync(user => user.Email == "new.admin@example.com");
        Assert.Equal(UserRole.Admin, admin.Role);
        Assert.Equal(PasswordVerificationResult.Success, _hasher.VerifyHashedPassword(admin, admin.PasswordHash, Password));
        Assert.NotNull((await context.AdminInvitations.SingleAsync()).AcceptedAtUtc);
    }

    [Fact]
    public async Task AcceptInvitation_CodeCannotBeReused()
    {
        using var context = CreateContext();
        var boss = await AddSuperAdminAsync(context);
        var service = CreateService(context);
        await service.InviteAdminAsync(new InviteAdminDTO { Email = "new.admin@example.com" }, boss.Id, CancellationToken.None);
        var code = _emailSender.Sent[0].Code;
        await service.AcceptAdminInvitationAsync(Accept(code), CancellationToken.None);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.AcceptAdminInvitationAsync(Accept(code, username: "other"), CancellationToken.None));
    }

    [Fact]
    public async Task AcceptInvitation_WrongCode_IsRejectedAndNoUserCreated()
    {
        using var context = CreateContext();
        var boss = await AddSuperAdminAsync(context);
        var service = CreateService(context);
        await service.InviteAdminAsync(new InviteAdminDTO { Email = "new.admin@example.com" }, boss.Id, CancellationToken.None);
        var wrong = _emailSender.Sent[0].Code == "00000000" ? "11111111" : "00000000";

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.AcceptAdminInvitationAsync(Accept(wrong), CancellationToken.None));

        Assert.Equal(1, await context.Users.CountAsync());
        Assert.Equal(1, (await context.AdminInvitations.SingleAsync()).FailedAttempts);
    }

    [Fact]
    public async Task AcceptInvitation_TooManyWrongCodes_LocksInvitation()
    {
        using var context = CreateContext();
        var boss = await AddSuperAdminAsync(context);
        var service = CreateService(context);
        await service.InviteAdminAsync(new InviteAdminDTO { Email = "new.admin@example.com" }, boss.Id, CancellationToken.None);
        var realCode = _emailSender.Sent[0].Code;
        var wrong = realCode == "00000000" ? "11111111" : "00000000";

        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => service.AcceptAdminInvitationAsync(Accept(wrong), CancellationToken.None));
        }

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.AcceptAdminInvitationAsync(Accept(realCode), CancellationToken.None));
        Assert.Equal(1, await context.Users.CountAsync());
    }

    [Fact]
    public async Task AcceptInvitation_Expired_IsRejected()
    {
        using var context = CreateContext();
        var boss = await AddSuperAdminAsync(context);
        var service = CreateService(context);
        await service.InviteAdminAsync(new InviteAdminDTO { Email = "new.admin@example.com" }, boss.Id, CancellationToken.None);
        var invitation = await context.AdminInvitations.SingleAsync();
        invitation.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.AcceptAdminInvitationAsync(Accept(_emailSender.Sent[0].Code), CancellationToken.None));
    }

    [Fact]
    public async Task AcceptInvitation_CodeBoundToEmail_IsRejectedForAnotherEmail()
    {
        using var context = CreateContext();
        var boss = await AddSuperAdminAsync(context);
        var service = CreateService(context);
        await service.InviteAdminAsync(new InviteAdminDTO { Email = "new.admin@example.com" }, boss.Id, CancellationToken.None);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.AcceptAdminInvitationAsync(
                Accept(_emailSender.Sent[0].Code, email: "someone.else@example.com"), CancellationToken.None));
    }

    [Fact]
    public async Task InviteAdmin_AlreadyRegisteredEmail_ThrowsConflict()
    {
        using var context = CreateContext();
        var boss = await AddSuperAdminAsync(context);

        await Assert.ThrowsAsync<ConflictException>(
            () => CreateService(context).InviteAdminAsync(
                new InviteAdminDTO { Email = "BOSS@example.com" }, boss.Id, CancellationToken.None));
        Assert.Empty(_emailSender.Sent);
    }

    [Fact]
    public async Task InviteAdmin_Again_InvalidatesPreviousCode()
    {
        using var context = CreateContext();
        var boss = await AddSuperAdminAsync(context);
        var service = CreateService(context);
        var dto = new InviteAdminDTO { Email = "new.admin@example.com" };
        await service.InviteAdminAsync(dto, boss.Id, CancellationToken.None);
        await service.InviteAdminAsync(dto, boss.Id, CancellationToken.None);
        var (first, second) = (_emailSender.Sent[0].Code, _emailSender.Sent[1].Code);

        if (first != second)
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => service.AcceptAdminInvitationAsync(Accept(first), CancellationToken.None));
        }

        var result = await service.AcceptAdminInvitationAsync(Accept(second), CancellationToken.None);
        Assert.Equal("Admin", result.Role);
    }

    [Fact]
    public async Task InviteAdmin_EmailDeliveryFails_RevokesInvitation()
    {
        using var context = CreateContext();
        var boss = await AddSuperAdminAsync(context);

        await Assert.ThrowsAsync<NotificationDeliveryException>(
            () => CreateService(context, emailSender: new FailingEmailSender()).InviteAdminAsync(
                new InviteAdminDTO { Email = "new.admin@example.com" }, boss.Id, CancellationToken.None));

        Assert.NotNull((await context.AdminInvitations.SingleAsync()).RevokedAtUtc);
        await CreateService(context).InviteAdminAsync(
            new InviteAdminDTO { Email = "new.admin@example.com" }, boss.Id, CancellationToken.None);
    }

    [Fact]
    public async Task Login_SuperAdmin_TokenContainsSuperAdminAndAdminRoles()
    {
        using var context = CreateContext();
        var superAdmin = new User { Username = "root", Email = "root@example.com", Role = UserRole.SuperAdmin };
        superAdmin.PasswordHash = _hasher.HashPassword(superAdmin, Password);
        context.Users.Add(superAdmin);
        await context.SaveChangesAsync();

        var result = await CreateService(context).LoginAsync(
            new LoginDTO { UsernameOrEmail = "root", Password = Password }, CancellationToken.None);
        var roleClaims = ReadToken(result.Token).Claims
            .Where(claim => claim.Type == ClaimTypes.Role || claim.Type == "role")
            .Select(claim => claim.Value)
            .ToArray();

        Assert.Equal("SuperAdmin", result.Role);
        Assert.Contains("SuperAdmin", roleClaims);
        Assert.Contains("Admin", roleClaims);
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
    public async Task Refresh_RotatesTokenAndRejectsReuse()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        var registered = await service.RegisterAsync(Register(), CancellationToken.None);

        var refreshed = await service.RefreshAsync(registered.RefreshToken, CancellationToken.None);

        Assert.NotEqual(registered.RefreshToken, refreshed.RefreshToken);
        Assert.False(string.IsNullOrWhiteSpace(refreshed.Token));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.RefreshAsync(registered.RefreshToken, CancellationToken.None));
        var storedTokens = await context.RefreshTokens.ToListAsync();
        Assert.Equal(2, storedTokens.Count);
        Assert.Equal(1, storedTokens.Count(token => token.RevokedAtUtc.HasValue));
        Assert.Equal(1, storedTokens.Count(token => !token.RevokedAtUtc.HasValue));
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        var registered = await service.RegisterAsync(Register(), CancellationToken.None);

        await service.RevokeRefreshTokenAsync(registered.RefreshToken, CancellationToken.None);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.RefreshAsync(registered.RefreshToken, CancellationToken.None));
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
