using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Enums;
using CinemaGo.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CinemaGo.Web.Seeding;

// All operations are idempotent: running them repeatedly never creates duplicates or overwrites edited data.
public static class DatabaseSeeder
{
    private static readonly string[] Genres = ["Action", "Comedy", "Drama", "Horror", "Sci-Fi", "Thriller", "Animation"];

    private static readonly (string Title, int Duration, int Age, string[] Genres)[] Movies =
    [
        ("Dune", 155, 12, ["Sci-Fi", "Drama"]),
        ("Inception", 148, 12, ["Sci-Fi", "Thriller", "Action"]),
        ("The Dark Knight", 152, 16, ["Action", "Thriller", "Drama"]),
        ("Toy Story", 81, 0, ["Animation", "Comedy"]),
        ("The Conjuring", 112, 18, ["Horror", "Thriller"])
    ];

    // Rows × seats per row; the last row of every hall is VIP.
    private static readonly (string Name, int Rows, int SeatsPerRow)[] Halls =
    [
        ("Hall 1", 5, 8),
        ("Hall 2", 6, 10),
        ("Hall 3", 4, 6)
    ];

    public static async Task SeedReferenceDataAsync(CinemaDbContext context, CancellationToken cancellationToken = default)
    {
        foreach (var name in Genres)
        {
            if (!await context.Genres.AnyAsync(g => g.Name == name, cancellationToken))
            {
                context.Genres.Add(new Genre { Name = name });
            }
        }
        await context.SaveChangesAsync(cancellationToken);

        var genresByName = await context.Genres.ToDictionaryAsync(g => g.Name, cancellationToken);
        foreach (var (title, duration, age, genreNames) in Movies)
        {
            var movie = await context.Movies
                .Include(m => m.MovieGenres)
                .FirstOrDefaultAsync(m => m.Title == title, cancellationToken);

            if (movie is null)
            {
                movie = new Movie { Title = title, DurationMinutes = duration, AgeRating = age };
                context.Movies.Add(movie);
                foreach (var genreName in genreNames)
                {
                    movie.MovieGenres.Add(new MovieGenre { Genre = genresByName[genreName] });
                }
            }
        }
        await context.SaveChangesAsync(cancellationToken);

        foreach (var (name, rows, seatsPerRow) in Halls)
        {
            var hall = await context.Halls
                .Include(h => h.Seats)
                .FirstOrDefaultAsync(h => h.Name == name, cancellationToken);

            if (hall is null)
            {
                hall = new Hall { Name = name, Capacity = rows * seatsPerRow };
                context.Halls.Add(hall);
            }

            for (var row = 1; row <= rows; row++)
            {
                for (var number = 1; number <= seatsPerRow; number++)
                {
                    if (!hall.Seats.Any(s => s.RowNumber == row && s.SeatNumber == number))
                    {
                        hall.Seats.Add(new Seat { RowNumber = row, SeatNumber = number, IsVip = row == rows });
                    }
                }
            }
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    // Creates the first Admin only when no Admin exists and credentials were supplied via configuration
    // (user secrets / environment variables). Returns true when an account was created.
    public static async Task<bool> EnsureInitialAdminAsync(
        CinemaDbContext context,
        IPasswordHasher<User> passwordHasher,
        string? username,
        string? email,
        string? password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            return false;
        }

        if (await context.Users.AnyAsync(u => u.Role == UserRole.Admin, cancellationToken))
        {
            return false;
        }

        username = username.Trim();
        email = email.Trim();

        if (username.Length < 3 || password.Length < 12 || !email.Contains('@'))
        {
            throw new InvalidOperationException(
                "Initial admin settings are invalid: username needs 3+ characters, a valid email and a password of 12+ characters.");
        }

        if (await context.Users.AnyAsync(u => u.Username == username || u.Email == email, cancellationToken))
        {
            throw new InvalidOperationException(
                "Initial admin username or email is already used by a non-admin account; refusing to promote it automatically.");
        }

        var admin = new User { Username = username, Email = email, Role = UserRole.Admin };
        admin.PasswordHash = passwordHasher.HashPassword(admin, password);
        context.Users.Add(admin);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            context.Entry(admin).State = EntityState.Detached;
            if (await context.Users.AsNoTracking().AnyAsync(u => u.Role == UserRole.Admin, cancellationToken))
            {
                // Another instance created the admin at the same time.
                return false;
            }
            throw;
        }

        return true;
    }
}

