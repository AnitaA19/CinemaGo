using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CinemaGo.Domain.DTOs.AuthDTOs;
using CinemaGo.Domain.Entities;
using CinemaGo.Domain.Enums;
using CinemaGo.Domain.Exceptions;
using CinemaGo.Domain.Intefraces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CinemaGo.Application.Services.Implementations;

public class AuthService : Services.Interfaces.IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IConfiguration _configuration;

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher<User> passwordHasher,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
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

        if (username.Length < 3 || string.IsNullOrWhiteSpace(email))
        {
            throw new ValidationException("Username and email are required.");
        }

        var existingUsername =
            await _userRepository.GetUserByUsernameAsync(
                username, cancellationToken);

        if (existingUsername is not null)
        {
            throw new ConflictException("Username is already taken.");
        }

        var existingEmail =
            await _userRepository.GetUserByEmailAsync(
                email, cancellationToken);

        if (existingEmail is not null)
        {
            throw new ConflictException("Email is already registered.");
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

        return await GenerateAuthResponseAsync(user, cancellationToken);
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

        return await GenerateAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponseDTO> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        var storedToken = await _refreshTokenRepository.GetByTokenHashAsync(
            HashRefreshToken(refreshToken), cancellationToken);
        var now = DateTime.UtcNow;

        if (storedToken is null || storedToken.RevokedAtUtc.HasValue || storedToken.ExpiresAtUtc <= now)
        {
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        storedToken.RevokedAtUtc = now;
        return await GenerateAuthResponseAsync(
            storedToken.User, cancellationToken, storedToken);
    }

    public async Task RevokeRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var storedToken = await _refreshTokenRepository.GetByTokenHashAsync(
            HashRefreshToken(refreshToken), cancellationToken);

        if (storedToken is null || storedToken.RevokedAtUtc.HasValue)
        {
            return;
        }

        storedToken.RevokedAtUtc = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthResponseDTO> GenerateAuthResponseAsync(
        User user,
        CancellationToken cancellationToken,
        RefreshToken? tokenToRotate = null)
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

        var now = DateTime.UtcNow;
        var expiresAtUtc = now.AddMinutes(expiresInMinutes);
        var refreshTokenLifetimeDays = int.TryParse(
            _configuration["RefreshToken:ExpiresInDays"], out var days)
            ? days
            : 7;

        if (refreshTokenLifetimeDays <= 0)
        {
            throw new InvalidOperationException(
                "Refresh token lifetime must be greater than zero days.");
        }

        var refreshTokenExpiresAtUtc = now.AddDays(refreshTokenLifetimeDays);
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

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

        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashRefreshToken(refreshToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = refreshTokenExpiresAtUtc
        };

        if (tokenToRotate is null)
        {
            await _refreshTokenRepository.AddAsync(refreshTokenEntity, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        else if (!await _refreshTokenRepository.TryRotateAsync(
            tokenToRotate, refreshTokenEntity, cancellationToken))
        {
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        return new AuthResponseDTO
        {
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role.ToString(),
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAtUtc = expiresAtUtc,
            RefreshToken = refreshToken,
            RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc
        };
    }

    private static string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}