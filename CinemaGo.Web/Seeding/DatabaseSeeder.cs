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

    // Poster images are hosted on Wikipedia (film poster files); admins can replace them via the movie form.
    private static readonly Dictionary<string, string> PosterUrls = new()
    {
        ["Dune"] = "https://en.wikipedia.org/wiki/Special:FilePath/Dune_(2021_film).jpg?width=500",
        ["Inception"] = "https://en.wikipedia.org/wiki/Special:FilePath/Inception_(2010)_theatrical_poster.jpg?width=500",
        ["The Dark Knight"] = "https://en.wikipedia.org/wiki/Special:FilePath/The_Dark_Knight_(2008_film).jpg?width=500",
        ["Toy Story"] = "https://en.wikipedia.org/wiki/Special:FilePath/Toy_Story.jpg?width=500",
        ["The Conjuring"] = "https://en.wikipedia.org/wiki/Special:FilePath/The_Conjuring_poster.jpg?width=500"
    };

    // Rows × seats per row
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
                .Include(m => m.Genres)
                .FirstOrDefaultAsync(m => m.Title == title, cancellationToken);

            if (movie is null)
            {
                movie = new Movie { Title = title, DurationMinutes = duration, AgeRating = age };
                context.Movies.Add(movie);
                foreach (var genreName in genreNames)
                {
                    movie.Genres.Add(genresByName[genreName]);
                }
            }

            if ((movie.PosterUrl is null || movie.PosterUrl.StartsWith("/posters/")) && PosterUrls.TryGetValue(title, out var posterUrl))
            {
                movie.PosterUrl = posterUrl;
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

    // Test data for development: users (password "Passw0rd!Demo1"), sessions, bookings and tickets. Skipped if demo users exist.
    public static async Task SeedDemoDataAsync(
        CinemaDbContext context,
        IPasswordHasher<User> passwordHasher,
        CancellationToken cancellationToken = default)
    {
        const string demoPassword = "Passw0rd!Demo1";

        if (await context.Users.AnyAsync(u => u.Email == "demo.admin@cinemago.test", cancellationToken))
        {
            return;
        }

        var users = new List<User>
        {
            new() { Username = "demo_admin", Email = "demo.admin@cinemago.test", Role = UserRole.Admin },
            new() { Username = "alice", Email = "alice@cinemago.test", Role = UserRole.Customer },
            new() { Username = "bob", Email = "bob@cinemago.test", Role = UserRole.Customer },
            new() { Username = "carol", Email = "carol@cinemago.test", Role = UserRole.Customer },
            new() { Username = "dave", Email = "dave@cinemago.test", Role = UserRole.Customer }
        };
        foreach (var user in users)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, demoPassword);
        }
        context.Users.AddRange(users);

        var movies = await context.Movies.OrderBy(m => m.Id).ToListAsync(cancellationToken);
        var halls = await context.Halls.Include(h => h.Seats).OrderBy(h => h.Id).ToListAsync(cancellationToken);
        if (movies.Count == 0 || halls.Count == 0)
        {
            return;
        }

        var today = DateTime.UtcNow.Date;
        var sessions = new List<Session>();
        // Past (completed), today/upcoming (scheduled) and one cancelled session.
        for (var i = 0; i < 9; i++)
        {
            var dayOffset = i switch { < 2 => -2 - i, < 7 => i - 2, _ => i - 5 };
            var status = i < 2 ? SessionStatus.Completed : i == 8 ? SessionStatus.Cancelled : SessionStatus.Scheduled;
            sessions.Add(new Session
            {
                Movie = movies[i % movies.Count],
                Hall = halls[i % halls.Count],
                StartsAt = today.AddDays(dayOffset).AddHours(12 + (i % 4) * 3),
                StandardPrice = 8 + (i % 3) * 2,
                VipPrice = 14 + (i % 3) * 2,
                Status = status
            });
        }
        context.Sessions.AddRange(sessions);
        await context.SaveChangesAsync(cancellationToken);

        var customers = users.Where(u => u.Role == UserRole.Customer).ToList();
        var random = new Random(42);
        foreach (var session in sessions)
        {
            var seats = session.Hall.Seats.OrderBy(s => s.RowNumber).ThenBy(s => s.SeatNumber).ToList();
            // Take a mix of regular and VIP seats; VIP is the last row.
            var chosen = seats.Take(6).Concat(seats.TakeLast(2)).ToList();
            for (var n = 0; n < chosen.Count; n++)
            {
                var seat = chosen[n];
                var cancelled = session.Status == SessionStatus.Cancelled || n % 5 == 4;
                var booking = new Booking
                {
                    User = customers[random.Next(customers.Count)],
                    Session = session,
                    Seat = seat,
                    Status = cancelled ? BookingStatus.Cancelled : BookingStatus.Active,
                    CreatedAt = session.StartsAt.AddDays(-random.Next(1, 5))
                };
                context.Bookings.Add(booking);

                if (!cancelled)
                {
                    booking.Ticket = new Ticket
                    {
                        TicketCode = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
                        FinalPrice = seat.IsVip ? session.VipPrice : session.StandardPrice
                    };
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    // Creates the single SuperAdmin account from user secrets or environment variables.
    public static async Task<bool> EnsureInitialSuperAdminAsync(
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

        if (await context.Users.AnyAsync(u => u.Role == UserRole.SuperAdmin, cancellationToken))
        {
            return false;
        }

        username = username.Trim();
        email = email.Trim();

        if (username.Length < 3 || password.Length < 12 || !email.Contains('@'))
        {
            throw new InvalidOperationException(
                "Initial super-admin settings are invalid: username needs 3+ characters, a valid email and a password of 12+ characters.");
        }

        if (await context.Users.AnyAsync(u => u.Username == username || u.Email == email, cancellationToken))
        {
            throw new InvalidOperationException(
                "Initial super-admin username or email is already used by another account; refusing to promote it automatically.");
        }

        var superAdmin = new User { Username = username, Email = email, Role = UserRole.SuperAdmin };
        superAdmin.PasswordHash = passwordHasher.HashPassword(superAdmin, password);
        context.Users.Add(superAdmin);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            context.Entry(superAdmin).State = EntityState.Detached;
            if (await context.Users.AsNoTracking().AnyAsync(u => u.Role == UserRole.SuperAdmin, cancellationToken))
            {
                // Another instance created the admin at the same time.
                return false;
            }
            throw;
        }

        return true;
    }
}

