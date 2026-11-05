using CinemaGo.Domain.DTOs.AuthDTOs;
using CinemaGo.Domain.DTOs.BookingDTOs;
using CinemaGo.Domain.DTOs.GenreDTOs;
using CinemaGo.Domain.DTOs.HallDTOs;
using CinemaGo.Domain.DTOs.SeatDTOs;
using CinemaGo.Domain.DTOs.MovieDTOs;
using CinemaGo.Domain.DTOs.SessionDTOs;
using FluentValidation;

namespace CinemaGo.Application.Validation;

public sealed class RegisterDtoValidator : AbstractValidator<RegisterDTO>
{
    public RegisterDtoValidator()
    {
        RuleFor(dto => dto.Username)
            .NotEmpty()
            .Length(3, 100)
            .Matches(@"^[A-Za-z0-9_.-]+$")
            .WithMessage("Username may contain only letters, digits, '_', '.' and '-'.");

        RuleFor(dto => dto.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(dto => dto.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128);
    }
}

public sealed class InviteAdminDtoValidator : AbstractValidator<InviteAdminDTO>
{
    public InviteAdminDtoValidator()
    {
        RuleFor(dto => dto.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);
    }
}

public sealed class AcceptAdminInvitationDtoValidator : AbstractValidator<AcceptAdminInvitationDTO>
{
    public AcceptAdminInvitationDtoValidator()
    {
        RuleFor(dto => dto.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(dto => dto.Username)
            .NotEmpty()
            .Length(3, 100)
            .Matches(@"^[A-Za-z0-9_.-]+$")
            .WithMessage("Username may contain only letters, digits, '_', '.' and '-'.");

        RuleFor(dto => dto.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128);

        RuleFor(dto => dto.Code)
            .NotEmpty()
            .MaximumLength(32);
    }
}

public sealed class LoginDtoValidator : AbstractValidator<LoginDTO>
{
    public LoginDtoValidator()
    {
        RuleFor(dto => dto.UsernameOrEmail)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(dto => dto.Password)
            .NotEmpty()
            .MaximumLength(128);
    }
}

internal static class MoviePosterUrl
{
    public static bool IsValid(string? url) =>
        string.IsNullOrWhiteSpace(url)
        || (url.StartsWith('/') && !url.StartsWith("//"))
        || (Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));
}

public sealed class CreateMovieDtoValidator : AbstractValidator<CreateMovieDTO>
{
    public CreateMovieDtoValidator()
    {
        RuleFor(dto => dto.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(dto => dto.DurationMinutes)
            .InclusiveBetween(1, 1000);

        RuleFor(dto => dto.AgeRating)
            .InclusiveBetween(0, 21);

        RuleFor(dto => dto.PosterUrl)
            .MaximumLength(500)
            .Must(MoviePosterUrl.IsValid).WithMessage("Poster URL must be an http(s) URL or a site-relative path starting with '/'.");

        RuleFor(dto => dto.GenreIds)
            .NotNull()
            .Must(ids => ids == null || ids.Count <= 20).WithMessage("A movie can have at most 20 genres.");

        RuleForEach(dto => dto.GenreIds).GreaterThan(0);
    }
}

public sealed class UpdateMovieDtoValidator : AbstractValidator<UpdateMovieDTO>
{
    public UpdateMovieDtoValidator()
    {
        RuleFor(dto => dto.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(dto => dto.DurationMinutes)
            .InclusiveBetween(1, 1000);

        RuleFor(dto => dto.AgeRating)
            .InclusiveBetween(0, 21);

        RuleFor(dto => dto.PosterUrl)
            .MaximumLength(500)
            .Must(MoviePosterUrl.IsValid).WithMessage("Poster URL must be an http(s) URL or a site-relative path starting with '/'.");

        RuleFor(dto => dto.GenreIds)
            .NotNull()
            .Must(ids => ids == null || ids.Count <= 20).WithMessage("A movie can have at most 20 genres.");

        RuleForEach(dto => dto.GenreIds).GreaterThan(0);
    }
}

public sealed class CreateBookingDtoValidator : AbstractValidator<CreateBookingDTO>
{
    public CreateBookingDtoValidator()
    {
        RuleFor(dto => dto.SessionId).GreaterThan(0);
        RuleFor(dto => dto.SeatId).GreaterThan(0);
    }
}

public sealed class CreateBookingsDtoValidator : AbstractValidator<CreateBookingsDTO>
{
    public CreateBookingsDtoValidator()
    {
        RuleFor(dto => dto.SessionId).GreaterThan(0);
        RuleFor(dto => dto.SeatIds).NotNull().Must(ids => ids is { Count: >= 1 and <= 20 });
        RuleForEach(dto => dto.SeatIds).GreaterThan(0);
        RuleFor(dto => dto.SeatIds).Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithMessage("Seat IDs must be unique.");
    }
}

public sealed class CreateSessionDtoValidator : AbstractValidator<CreateSessionDTO>
{
    public CreateSessionDtoValidator()
    {
        RuleFor(dto => dto.MovieId).GreaterThan(0);
        RuleFor(dto => dto.HallId).GreaterThan(0);

        RuleFor(dto => dto.StartsAt)
            .Cascade(CascadeMode.Stop)
            .NotEqual(default(DateTime)).WithMessage("Session start time must be specified.")
            .Must(value => value.Kind != DateTimeKind.Unspecified)
            .WithMessage("Session start time must include a UTC offset (e.g. 2030-01-01T18:00:00Z).")
            .Must(value => value.ToUniversalTime() > DateTime.UtcNow)
            .WithMessage("Session start time must be in the future.");

        RuleFor(dto => dto.StandardPrice)
            .InclusiveBetween(0m, 100000m)
            .Must(value => decimal.Round(value, 2) == value)
            .WithMessage("StandardPrice can have at most 2 decimal places.");

        RuleFor(dto => dto.VipPrice)
            .InclusiveBetween(0m, 100000m)
            .Must(value => decimal.Round(value, 2) == value)
            .WithMessage("VipPrice can have at most 2 decimal places.")
            .Must((dto, value) => value >= dto.StandardPrice)
            .WithMessage("VipPrice cannot be lower than StandardPrice.");
    }
}

public sealed class UpdateSessionDtoValidator : AbstractValidator<UpdateSessionDTO>
{
    public UpdateSessionDtoValidator()
    {
        RuleFor(dto => dto.MovieId).GreaterThan(0);
        RuleFor(dto => dto.HallId).GreaterThan(0);

        RuleFor(dto => dto.StartsAt)
            .Cascade(CascadeMode.Stop)
            .NotEqual(default(DateTime)).WithMessage("Session start time must be specified.")
            .Must(value => value.Kind != DateTimeKind.Unspecified)
            .WithMessage("Session start time must include a UTC offset (e.g. 2030-01-01T18:00:00Z).")
            .Must(value => value.ToUniversalTime() > DateTime.UtcNow)
            .WithMessage("Session start time must be in the future.");

        RuleFor(dto => dto.StandardPrice)
            .InclusiveBetween(0m, 100000m)
            .Must(value => decimal.Round(value, 2) == value)
            .WithMessage("StandardPrice can have at most 2 decimal places.");

        RuleFor(dto => dto.VipPrice)
            .InclusiveBetween(0m, 100000m)
            .Must(value => decimal.Round(value, 2) == value)
            .WithMessage("VipPrice can have at most 2 decimal places.")
            .Must((dto, value) => value >= dto.StandardPrice)
            .WithMessage("VipPrice cannot be lower than StandardPrice.");
    }
}

public sealed class CreateGenreDtoValidator : AbstractValidator<CreateGenreDTO>
{
    public CreateGenreDtoValidator()
    {
        RuleFor(dto => dto.Name).NotEmpty().MaximumLength(100);
    }
}

public sealed class UpdateGenreDtoValidator : AbstractValidator<UpdateGenreDTO>
{
    public UpdateGenreDtoValidator()
    {
        RuleFor(dto => dto.Name).NotEmpty().MaximumLength(100);
    }
}

public sealed class CreateHallDtoValidator : AbstractValidator<CreateHallDTO>
{
    public CreateHallDtoValidator()
    {
        RuleFor(dto => dto.Name).NotEmpty().MaximumLength(100);
        RuleFor(dto => dto.Capacity).InclusiveBetween(1, 10000);
    }
}

public sealed class UpdateHallDtoValidator : AbstractValidator<UpdateHallDTO>
{
    public UpdateHallDtoValidator()
    {
        RuleFor(dto => dto.Name).NotEmpty().MaximumLength(100);
        RuleFor(dto => dto.Capacity).InclusiveBetween(1, 10000);
    }
}

public sealed class CreateSeatDtoValidator : AbstractValidator<CreateSeatDTO>
{
    public CreateSeatDtoValidator()
    {
        RuleFor(dto => dto.HallId).GreaterThan(0);
        RuleFor(dto => dto.RowNumber).InclusiveBetween(1, 1000);
        RuleFor(dto => dto.SeatNumber).InclusiveBetween(1, 1000);
    }
}

public sealed class UpdateSeatDtoValidator : AbstractValidator<UpdateSeatDTO>
{
    public UpdateSeatDtoValidator()
    {
        RuleFor(dto => dto.RowNumber).InclusiveBetween(1, 1000);
        RuleFor(dto => dto.SeatNumber).InclusiveBetween(1, 1000);
    }
}


