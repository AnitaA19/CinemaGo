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
    private const int MaxInvitationCodeAttempts = 5;

    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IAdminInvitationRepository _invitationRepository;
    private readonly IAdminInvitationEmailSender _invitationEmailSender;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IConfiguration _configuration;

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IAdminInvitationRepository invitationRepository,
        IAdminInvitationEmailSender invitationEmailSender,
        IUnitOfWork unitOfWork,
        IPasswordHasher<User> passwordHasher,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _invitationRepository = invitationRepository;
        _invitationEmailSender = invitationEmailSender;
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

    public async Task<AdminInvitationDTO> InviteAdminAsync(
        InviteAdminDTO dto,
        int invitedByUserId,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(dto.Email);

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            throw new ValidationException("A valid email is required.");
        }

        if (await _userRepository.GetUserByEmailAsync(email, cancellationToken) is not null)
        {
            throw new ConflictException("Email is already registered.");
        }

        var now = DateTime.UtcNow;

        // Re-inviting the same email replaces the previous invitation (acts as "resend").
        var existing = await _invitationRepository.GetActiveByEmailAsync(email, cancellationToken);
        if (existing is not null)
        {
            existing.RevokedAtUtc = now;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var lifetimeMinutes = int.TryParse(
            _configuration["AdminInvitation:ExpiresInMinutes"], out var minutes) ? minutes : 10;
        if (lifetimeMinutes <= 0)
        {
            throw new InvalidOperationException(
                "Admin invitation lifetime must be greater than zero minutes.");
        }

        var code = RandomNumberGenerator.GetInt32(0, 100_000_000).ToString("D8");
        var invitation = new AdminInvitation
        {
            Email = email,
            CodeHash = HashInvitationCode(email, code),
            CreatedByUserId = invitedByUserId,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(lifetimeMinutes)
        };

        await _invitationRepository.AddAsync(invitation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var acceptUrl = _configuration["AdminInvitation:AcceptUrl"];
        if (string.IsNullOrWhiteSpace(acceptUrl)
            || !Uri.TryCreate(acceptUrl, UriKind.Absolute, out var acceptUri)
            || acceptUri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException(
                "AdminInvitation:AcceptUrl must be configured with an absolute http(s) URL of the invitation page.");
        }

        var link = $"{acceptUrl}{(acceptUrl.Contains('?') ? '&' : '?')}email={Uri.EscapeDataString(email)}";

        try
        {
            await _invitationEmailSender.SendAsync(
                email, code, link, invitation.ExpiresAtUtc, cancellationToken);
        }
        catch
        {
            // An invitation nobody received must not block a retry.
            invitation.RevokedAtUtc = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        return new AdminInvitationDTO
        {
            InvitationId = invitation.Id,
            Email = invitation.Email,
            ExpiresAtUtc = invitation.ExpiresAtUtc
        };
    }

    public async Task<AuthResponseDTO> AcceptAdminInvitationAsync(
        AcceptAdminInvitationDTO dto,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(dto.Email);
        var username = dto.Username.Trim();
        var code = dto.Code.Trim();

        if (string.IsNullOrWhiteSpace(email) || username.Length < 3
            || string.IsNullOrWhiteSpace(dto.Password) || string.IsNullOrWhiteSpace(code))
        {
            throw new ValidationException("Email, username, password and code are required.");
        }

        var (outcome, admin) = await _unitOfWork.ExecuteInSerializableTransactionAsync(
            async () =>
            {
                var now = DateTime.UtcNow;
                var invitation = await _invitationRepository.GetActiveByEmailAsync(email, cancellationToken);

                if (invitation is null
                    || invitation.ExpiresAtUtc <= now
                    || invitation.FailedAttempts >= MaxInvitationCodeAttempts)
                {
                    return (AcceptOutcome.Invalid, (User?)null);
                }

                var expectedHash = Encoding.UTF8.GetBytes(invitation.CodeHash);
                var actualHash = Encoding.UTF8.GetBytes(HashInvitationCode(email, code));
                if (!CryptographicOperations.FixedTimeEquals(expectedHash, actualHash))
                {
                    invitation.FailedAttempts++;
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    return (AcceptOutcome.Invalid, (User?)null);
                }

                if (await _userRepository.GetUserByEmailAsync(email, cancellationToken) is not null)
                {
                    return (AcceptOutcome.EmailTaken, (User?)null);
                }

                if (await _userRepository.GetUserByUsernameAsync(username, cancellationToken) is not null)
                {
                    return (AcceptOutcome.UsernameTaken, (User?)null);
                }

                var newAdmin = new User
                {
                    Username = username,
                    Email = email,
                    Role = UserRole.Admin
                };
                newAdmin.PasswordHash = _passwordHasher.HashPassword(newAdmin, dto.Password);

                await _userRepository.AddUserAsync(newAdmin, cancellationToken);
                invitation.AcceptedAtUtc = now;
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return (AcceptOutcome.Accepted, newAdmin);
            },
            cancellationToken);

        return outcome switch
        {
            AcceptOutcome.Accepted => await GenerateAuthResponseAsync(admin!, cancellationToken),
            AcceptOutcome.EmailTaken => throw new ConflictException("Email is already registered."),
            AcceptOutcome.UsernameTaken => throw new ConflictException("Username is already taken."),
            _ => throw new UnauthorizedAccessException("Invalid or expired invitation code.")
        };
    }

    private enum AcceptOutcome
    {
        Invalid,
        EmailTaken,
        UsernameTaken,
        Accepted
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private string HashInvitationCode(string email, string code)
    {
        var key = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT signing key is not configured.");

        return Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(key),
            Encoding.UTF8.GetBytes($"{email}:{code}")));
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

        if (user.Role == UserRole.SuperAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, UserRole.Admin.ToString()));
        }

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