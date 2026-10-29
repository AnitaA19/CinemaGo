using CinemaGo.Domain.DTOs.AuthDTOs;
using CinemaGo.Domain.DTOs.BookingDTOs;
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
