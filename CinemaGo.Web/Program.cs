using System.Security.Claims;
using System.Text;
using CinemaGo.Application.Profiles;
using CinemaGo.Application.Services.Implementations;
using CinemaGo.Application.Services.Interfaces;
using CinemaGo.Application.Validation;
using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Intefraces;
using CinemaGo.Infrastructure.Data;
using CinemaGo.Infrastructure.Email;
using CinemaGo.Infrastructure.Repositories;
using CinemaGo.Infrastructure.UnitOfWork;
using CinemaGo.Web.Middleware;
using CinemaGo.Web.Seeding;
using CinemaGo.Web.Uploads;
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
        // Applies migrations and seeds data when "Seed:Enabled" is true and/or bootstrap credentials are supplied.
        private static async Task InitializeDatabaseAsync(WebApplication app)
        {
            var seedEnabled = app.Configuration.GetValue<bool>("Seed:Enabled");
            var superAdminSection = app.Configuration.GetSection("InitialSuperAdmin");
            var legacyAdminSection = app.Configuration.GetSection("InitialAdmin");
            var bootstrapSection = !string.IsNullOrWhiteSpace(superAdminSection["Password"])
                ? superAdminSection
                : legacyAdminSection;
            var superAdminConfigured = !string.IsNullOrWhiteSpace(bootstrapSection["Password"]);

            if (!seedEnabled && !superAdminConfigured)
            {
                return;
            }

            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            await context.Database.MigrateAsync();

            if (seedEnabled)
            {
                await DatabaseSeeder.SeedReferenceDataAsync(context);

                if (app.Configuration.GetValue<bool>("Seed:DemoData"))
                {
                    await DatabaseSeeder.SeedDemoDataAsync(context, scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>());
                }
            }

            if (superAdminConfigured)
            {
                var created = await DatabaseSeeder.EnsureInitialSuperAdminAsync(
                    context,
                    scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>(),
                    bootstrapSection["Username"],
                    bootstrapSection["Email"],
                    bootstrapSection["Password"]);

                if (created)
                {
                    app.Logger.LogInformation("Initial super-admin account created.");
                }
            }
        }

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddDbContext<CinemaDbContext>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString(
                        "DefaultConnection")));

            // Repositories
            builder.Services.AddSingleton(new PosterStorage(
                builder.Configuration["Uploads:Path"] ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "uploads")));
            builder.Services.AddScoped<IBookingRepository, BookingRepository>();
            builder.Services.AddScoped<IGenreRepository, GenreRepository>();
            builder.Services.AddScoped<IHallRepository, HallRepository>();
            builder.Services.AddScoped<IMovieRepository, MovieRepository>();
            builder.Services.AddScoped<ISeatRepository, SeatRepository>();
            builder.Services.AddScoped<ISessionRepository, SessionRepository>();
            builder.Services.AddScoped<ITicketRepository, TicketRepository>();
            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
            builder.Services.AddScoped<IAdminInvitationRepository, AdminInvitationRepository>();
            builder.Services.AddScoped<IAdminInvitationEmailSender, SmtpAdminInvitationEmailSender>();
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            builder.Services.AddScoped<ISessionCancellationEmailSender, SmtpSessionCancellationEmailSender>();

            // Application services
            builder.Services.AddScoped<IBookingService, BookingService>();
            builder.Services.AddScoped<ITicketService, TicketService>();
            builder.Services.AddScoped<IMovieService, MovieService>();
            builder.Services.AddScoped<ISessionService, SessionService>();
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IGenreService, GenreService>();
            builder.Services.AddScoped<IHallService, HallService>();
            builder.Services.AddScoped<ISeatService, SeatService>();
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

                        return new BadRequestObjectResult(new ValidationProblemDetails(errors)
                        {
                            Type = "about:blank",
                            Title = "One or more validation errors occurred.",
                            Status = StatusCodes.Status400BadRequest,
                            Detail = "The request contains invalid values.",
                            Instance = context.HttpContext.Request.Path
                        });
                    };
                });
            builder.Services.AddOpenApi();

            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc(
                    "v1",
                    new()
                    {
                        Title = "CinemaGo API",
                        Version = "v1"
                    });

                options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
                {
                    Type = Microsoft.OpenApi.SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Paste the JWT returned by /api/auth/login (without the 'Bearer ' prefix)."
                });

                options.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
                {
                    [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
                });
            });

            builder.Services.AddCors(options =>
                options.AddPolicy("LocalClients", policy =>
                    policy.WithOrigins(
                            "https://localhost:7086",
                            "http://localhost:5005",
                            "http://localhost:47219",
                            "https://cinemaforgo.netlify.app")
                        .AllowAnyHeader()
                        .AllowAnyMethod()));

            var app = builder.Build();

            InitializeDatabaseAsync(app).GetAwaiter().GetResult();

            app.UseExceptionHandler();
            app.UseMiddleware<ApiResponseMiddleware>();

                app.MapOpenApi();
                app.UseSwagger();
                app.UseSwaggerUI(options =>
                    options.SwaggerEndpoint(
                        "/swagger/v1/swagger.json",
                        "CinemaGo API v1"));
           

            app.UseCors("LocalClients");

            app.UseHttpsRedirection();

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
                    app.Services.GetRequiredService<PosterStorage>().RootPath),
                RequestPath = PosterStorage.RequestPath,
                OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable"
            });

            // Authentication must run before authorization.
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
