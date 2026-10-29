using System.Security.Claims;
using System.Text;
using CinemaGo.Application.Profiles;
using CinemaGo.Application.Services.Implementations;
using CinemaGo.Application.Services.Interfaces;
using CinemaGo.Application.Validation;
using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Intefraces;
using CinemaGo.Infrastructure.Data;
using CinemaGo.Infrastructure.Repositories;
using CinemaGo.Infrastructure.UnitOfWork;
using CinemaGo.Web.Middleware;
using CinemaGo.Web.Responses;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Cinema
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddDbContext<CinemaDbContext>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString(
                        "DefaultConnection")));

            // Repositories
            builder.Services.AddScoped<IBookingRepository, BookingRepository>();
            builder.Services.AddScoped<IGenreRepository, GenreRepository>();
            builder.Services.AddScoped<IHallRepository, HallRepository>();
            builder.Services.AddScoped<IMovieRepository, MovieRepository>();
            builder.Services.AddScoped<ISeatRepository, SeatRepository>();
            builder.Services.AddScoped<ISessionRepository, SessionRepository>();
            builder.Services.AddScoped<ITicketRepository, TicketRepository>();
            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Application services
            builder.Services.AddScoped<IBookingService, BookingService>();
            builder.Services.AddScoped<ITicketService, TicketService>();
            builder.Services.AddScoped<IMovieService, MovieService>();
            builder.Services.AddScoped<ISessionService, SessionService>();
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<FluentValidationActionFilter>();
            builder.Services.AddValidatorsFromAssemblyContaining<CreateMovieDtoValidator>();

            builder.Services.AddExceptionHandler<CinemaGo.Web.Middleware.GlobalExceptionHandler>();
            builder.Services.AddProblemDetails();

            // Secure password hashing
            builder.Services.AddScoped<IPasswordHasher<User>,
                PasswordHasher<User>>();

            builder.Services.AddAutoMapper(
                cfg => { },
                typeof(Program).Assembly,
                typeof(MovieService).Assembly);

            // JWT configuration
            var jwtSection = builder.Configuration.GetSection("Jwt");

            var secretKey = jwtSection["Key"]
                ?? throw new InvalidOperationException(
                    "JWT signing key is missing.");

            var issuer = jwtSection["Issuer"]
                ?? throw new InvalidOperationException(
                    "JWT issuer is missing.");

            var audience = jwtSection["Audience"]
                ?? throw new InvalidOperationException(
                    "JWT audience is missing.");

            if (Encoding.UTF8.GetByteCount(secretKey) < 32)
            {
                throw new InvalidOperationException(
                    "JWT signing key must be at least 32 bytes long.");
            }

            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters =
                        new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidIssuer = issuer,

                            ValidateAudience = true,
                            ValidAudience = audience,

                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = new SymmetricSecurityKey(
                                Encoding.UTF8.GetBytes(secretKey)),

                            ValidateLifetime = true,
                            ClockSkew = TimeSpan.FromSeconds(30),

                            NameClaimType = ClaimTypes.Name,
                            RoleClaimType = ClaimTypes.Role
                        };
                });

            builder.Services.AddAuthorization();

            builder.Services.AddControllers(options =>
                {
                    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
                    options.Filters.AddService<FluentValidationActionFilter>(-3000);
                })
                .ConfigureApiBehaviorOptions(options =>
                {
                    options.InvalidModelStateResponseFactory = context =>
                    {
                        var errors = context.ModelState
                            .Where(entry => entry.Value?.Errors.Count > 0)
                            .ToDictionary(
                                entry => entry.Key,
                                entry => entry.Value!.Errors
                                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                                        ? "The supplied value is invalid."
                                        : error.ErrorMessage)
                                    .ToArray());

                        return new BadRequestObjectResult(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "One or more validation errors occurred.",
                            Errors = errors
                        });
                    };
                });
            builder.Services.AddOpenApi();

            builder.Services.AddSwaggerGen(options =>
                options.SwaggerDoc(
                    "v1",
                    new()
                    {
                        Title = "CinemaGo API",
                        Version = "v1"
                    }));

            var app = builder.Build();

            app.UseExceptionHandler();
            app.UseMiddleware<ApiResponseMiddleware>();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.UseSwagger();
                app.UseSwaggerUI(options =>
                    options.SwaggerEndpoint(
                        "/swagger/v1/swagger.json",
                        "CinemaGo API v1"));
            }

            app.UseHttpsRedirection();

            // Authentication must run before authorization.
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}