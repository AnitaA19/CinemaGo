using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Enums;
using CinemaGo.Web.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CinemaGo.Tests.TestInfrastructure;

namespace CinemaGo.Tests.Persistence;

public sealed class DatabaseSeederTests
{
    private readonly TestDb _db = new();
    private readonly PasswordHasher<User> _hasher = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task SeedReferenceData_CreatesGenresMoviesHallsAndSeats()
    {
        await DatabaseSeeder.SeedReferenceDataAsync(_db.Context);

        Assert.True(await _db.Context.Genres.CountAsync() >= 5);
        Assert.True(await _db.Context.Movies.CountAsync() >= 3);
        Assert.True(await _db.Context.MovieGenres.AnyAsync());
        Assert.Equal(3, await _db.Context.Halls.CountAsync());
        foreach (var hall in await _db.Context.Halls.Include(h => h.Seats).ToListAsync())
        {
            Assert.Equal(hall.Capacity, hall.Seats.Count);
            Assert.Contains(hall.Seats, s => s.IsVip);
        }
    }

    [Fact]
    public async Task SeedReferenceData_RunTwice_DoesNotDuplicate()
    {
        await DatabaseSeeder.SeedReferenceDataAsync(_db.Context);
        var counts = await Counts();

        await DatabaseSeeder.SeedReferenceDataAsync(_db.Context);

        Assert.Equal(counts, await Counts());
    }

    [Fact]
    public async Task SeedReferenceData_RestoresMissingSeats_WithoutTouchingExistingOnes()
    {
        await DatabaseSeeder.SeedReferenceDataAsync(_db.Context);
        var seatCount = await _db.Context.Seats.CountAsync();
        var removed = await _db.Context.Seats.FirstAsync();
        _db.Context.Seats.Remove(removed);
        await _db.Context.SaveChangesAsync();

        await DatabaseSeeder.SeedReferenceDataAsync(_db.Context);

        Assert.Equal(seatCount, await _db.Context.Seats.CountAsync());
    }

    private async Task<(int, int, int, int, int)> Counts() => (
        await _db.Context.Genres.CountAsync(),
        await _db.Context.Movies.CountAsync(),
        await _db.Context.MovieGenres.CountAsync(),
        await _db.Context.Halls.CountAsync(),
        await _db.Context.Seats.CountAsync());

    [Fact]
    public async Task InitialAdmin_CreatesAdminWithHashedPassword()
    {
        var created = await DatabaseSeeder.EnsureInitialAdminAsync(_db.Context, _hasher, "admin", "admin@cinema.test", "S3cure-Passw0rd!");

        Assert.True(created);
        var admin = await _db.Context.Users.SingleAsync();
        Assert.Equal(UserRole.Admin, admin.Role);
        Assert.NotEqual("S3cure-Passw0rd!", admin.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, _hasher.VerifyHashedPassword(admin, admin.PasswordHash, "S3cure-Passw0rd!"));
    }

    [Fact]
    public async Task InitialAdmin_RunTwice_CreatesOnlyOne()
    {
        await DatabaseSeeder.EnsureInitialAdminAsync(_db.Context, _hasher, "admin", "admin@cinema.test", "S3cure-Passw0rd!");
        var again = await DatabaseSeeder.EnsureInitialAdminAsync(_db.Context, _hasher, "other", "other@cinema.test", "S3cure-Passw0rd!");

        Assert.False(again);
        Assert.Equal(1, await _db.Context.Users.CountAsync());
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("admin", "admin@cinema.test", "")]
    [InlineData("", "admin@cinema.test", "S3cure-Passw0rd!")]
    public async Task InitialAdmin_NotConfigured_DoesNothing(string? user, string? email, string? password)
    {
        Assert.False(await DatabaseSeeder.EnsureInitialAdminAsync(_db.Context, _hasher, user, email, password));
        Assert.Empty(_db.Context.Users);
    }

    [Fact]
    public async Task InitialAdmin_WeakPassword_IsRejected()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DatabaseSeeder.EnsureInitialAdminAsync(_db.Context, _hasher, "admin", "admin@cinema.test", "short"));
        Assert.Empty(_db.Context.Users);
    }

    [Fact]
    public async Task InitialAdmin_ExistingCustomerWithSameEmail_IsNotPromoted()
    {
        _db.Context.Users.Add(new User { Username = "bob", Email = "admin@cinema.test", PasswordHash = "x", Role = UserRole.Customer });
        await _db.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DatabaseSeeder.EnsureInitialAdminAsync(_db.Context, _hasher, "admin", "admin@cinema.test", "S3cure-Passw0rd!"));
        Assert.Equal(UserRole.Customer, (await _db.Context.Users.SingleAsync()).Role);
    }
}
