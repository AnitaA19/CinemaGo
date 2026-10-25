using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CinemaGo.Domain.DTOs.AuthDTOs;
using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Enums;
using CinemaGo.Domain.Intefraces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CinemaGo.Application.Services.Implementations;

public class AuthService : Services.Interfaces.IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IConfiguration _configuration;

    public AuthService(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher<User> passwordHasher,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
    }

    public async Task<AuthResponseDTO> RegisterAsync(
        RegisterDTO dto,
        CancellationToken cancellationToken)
    {
        var username = dto.Username.Trim();
        var email = dto.Email.Trim();

        var existingUsername =
            await _userRepository.GetUserByUsernameAsync(
                username, cancellationToken);

        if (existingUsername is not null)
        {
            throw new ArgumentException("Username is already taken.");
        }

        var existingEmail =
            await _userRepository.GetUserByEmailAsync(
                email, cancellationToken);

        if (existingEmail is not null)
        {
            throw new ArgumentException("Email is already registered.");
        }

        var user = new User
        {
            Username = username,
            Email = email,
            Role = UserRole.Customer
        };

        user.PasswordHash = _passwordHasher.HashPassword(
            user, dto.Password);

        await _userRepository.AddUserAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return GenerateAuthResponse(user);
    }

    public async Task<AuthResponseDTO> LoginAsync(
        LoginDTO dto,
        CancellationToken cancellationToken)
    {
        var login = dto.UsernameOrEmail.Trim();

        var user = login.Contains('@')
            ? await _userRepository.GetUserByEmailAsync(
                login, cancellationToken)
            : await _userRepository.GetUserByUsernameAsync(
                login, cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedAccessException(
                "Invalid username/email or password.");
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            dto.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAccessException(
                "Invalid username/email or password.");
        }

        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(
                user, dto.Password);

            _userRepository.UpdateUserAsync(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return GenerateAuthResponse(user);
    }

    private AuthResponseDTO GenerateAuthResponse(User user)
    {
        var jwtSection = _configuration.GetSection("Jwt");

        var secretKey = jwtSection["Key"]
            ?? throw new InvalidOperationException(
                "JWT signing key is not configured.");

        var issuer = jwtSection["Issuer"]
            ?? throw new InvalidOperationException(
                "JWT issuer is not configured.");

        var audience = jwtSection["Audience"]
            ?? throw new InvalidOperationException(
                "JWT audience is not configured.");

        var expiresInMinutes = int.TryParse(
            jwtSection["ExpiresInMinutes"], out var minutes)
            ? minutes
            : 60;

        if (Encoding.UTF8.GetByteCount(secretKey) < 32)
        {
            throw new InvalidOperationException(
                "JWT signing key must be at least 32 bytes long.");
        }

        var expiresAtUtc = DateTime.UtcNow.AddMinutes(expiresInMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(secretKey));

        var credentials = new SigningCredentials(
            key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new AuthResponseDTO
        {
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role.ToString(),
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAtUtc = expiresAtUtc
        };
    }
}