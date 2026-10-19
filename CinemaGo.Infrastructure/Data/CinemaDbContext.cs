using CinemaGo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CinemaGo.Infrastructure.Data;

public class CinemaDbContext : DbContext
{
    public CinemaDbContext(DbContextOptions<CinemaDbContext> options) : base(options)
    {
    }

    public DbSet<Movie> Movies { get; set; }
    public DbSet<Genre> Genres { get; set; }
    public DbSet<MovieGenre> MovieGenres { get; set; }
    public DbSet<Session> Sessions { get; set; }
    public DbSet<Hall> Halls { get; set; }
    public DbSet<Seat> Seats { get; set; }
    public DbSet<Ticket> Tickets { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Booking> Bookings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Movie>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Title).IsRequired().HasMaxLength(200);
            entity.Property(m => m.DurationMinutes).IsRequired();
            entity.Property(m => m.AgeRating).IsRequired();
            entity.HasCheckConstraint("CK_Movies_DurationMinutes", "[DurationMinutes] > 0");
            entity.HasCheckConstraint("CK_Movies_AgeRating", "[AgeRating] >= 0 AND [AgeRating] <= 21");
        });

        modelBuilder.Entity<Genre>(entity =>
        {
            entity.HasKey(g => g.Id);
            entity.Property(g => g.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(g => g.Name).IsUnique();
        });

        modelBuilder.Entity<MovieGenre>(entity =>
        {
            entity.HasKey(mg => new { mg.MovieId, mg.GenreId });

            entity.HasOne(mg => mg.Movie)
                .WithMany(m => m.MovieGenres)
                .HasForeignKey(mg => mg.MovieId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(mg => mg.Genre)
                .WithMany(g => g.MovieGenres)
                .HasForeignKey(mg => mg.GenreId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Hall>(entity =>
        {
            entity.HasKey(h => h.Id);
            entity.Property(h => h.Name).IsRequired().HasMaxLength(100);
            entity.Property(h => h.Capacity).IsRequired();
            entity.HasIndex(h => h.Name).IsUnique();
            entity.HasCheckConstraint("CK_Halls_Capacity", "[Capacity] > 0");

            entity.HasMany(h => h.Seats)
                .WithOne(s => s.Hall)
                .HasForeignKey(s => s.HallId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(h => h.Sessions)
                .WithOne(s => s.Hall)
                .HasForeignKey(s => s.HallId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Seat>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.RowNumber).IsRequired();
            entity.Property(s => s.SeatNumber).IsRequired();
            entity.Property(s => s.IsVip).IsRequired();
            entity.HasIndex(s => new { s.HallId, s.RowNumber, s.SeatNumber }).IsUnique();
            entity.HasCheckConstraint("CK_Seats_RowNumber", "[RowNumber] > 0");
            entity.HasCheckConstraint("CK_Seats_SeatNumber", "[SeatNumber] > 0");
        });

        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.StartsAt).IsRequired();
            entity.Property(s => s.StandardPrice).HasPrecision(18, 2).IsRequired();
            entity.Property(s => s.VipPrice).HasPrecision(18, 2).IsRequired();
            entity.Property(s => s.Status).IsRequired();
            entity.HasCheckConstraint("CK_Sessions_StandardPrice", "[StandardPrice] >= 0");
            entity.HasCheckConstraint("CK_Sessions_VipPrice", "[VipPrice] >= [StandardPrice]");

            entity.HasOne(s => s.Movie)
                .WithMany(m => m.Sessions)
                .HasForeignKey(s => s.MovieId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.Hall)
                .WithMany(h => h.Sessions)
                .HasForeignKey(s => s.HallId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(s => s.Bookings)
                .WithOne(b => b.Session)
                .HasForeignKey(b => b.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Username).IsRequired().HasMaxLength(100);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(256);
            entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(512);
            entity.Property(u => u.Role).IsRequired();
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex(u => u.Username).IsUnique();

            entity.HasMany(u => u.Bookings)
                .WithOne(b => b.User)
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(b => b.Id);
            entity.Property(b => b.Status).IsRequired();
            entity.Property(b => b.CreatedAt).IsRequired();
            entity.HasIndex(b => new { b.SessionId, b.SeatId }).IsUnique();

            entity.HasOne(b => b.Session)
                .WithMany(s => s.Bookings)
                .HasForeignKey(b => b.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(b => b.Seat)
                .WithMany(s => s.Bookings)
                .HasForeignKey(b => b.SeatId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.User)
                .WithMany(u => u.Bookings)
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.TicketCode).IsRequired().HasMaxLength(64);
            entity.Property(t => t.FinalPrice).HasPrecision(18, 2).IsRequired();
            entity.HasCheckConstraint("CK_Tickets_FinalPrice", "[FinalPrice] >= 0");
            entity.HasIndex(t => t.TicketCode).IsUnique();
            entity.HasIndex(t => t.BookingId).IsUnique();

            entity.HasOne(t => t.Booking)
                .WithOne(b => b.Ticket)
                .HasForeignKey<Ticket>(t => t.BookingId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
