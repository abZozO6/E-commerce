
using System.Net.Mail;
using System.Text;
using BCrypt.Net;
using Ecommerce.Application_BAL_.DTOs.Auth;
using Ecommerce.Application_BAL_.Interfaces;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;

namespace Ecommerce.Application_BAL_.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    private const int MaxFailedLoginAttempts = 5;
    private const int LockoutDurationMinutes = 15;

    public AuthService(
        IUserRepository userRepository,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _userRepository = userRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var fullName = dto.FullName?.Trim();
        var email = NormalizeEmail(dto.Email);
        var password = dto.Password;

        ValidateFullName(fullName);
        ValidateEmail(email);
        ValidatePassword(password);

        if (await _userRepository.EmailExistsAsync(email))
        {
            throw new InvalidOperationException(
                "An account with this email already exists.");
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, 12);

        var user = new User
        {
            FullName = fullName!,
            Email = email,
            PasswordHash = passwordHash,
            Role = "Customer",
            IsActive = true,
            IsDeleted = false,
            DeletedAt = null,
            CreatedAt = DateTime.UtcNow
        };

        var userId = await _userRepository.CreateAsync(user);

        user.UserId = userId;

        return CreateAuthResponse(user);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var email = NormalizeEmail(dto.Email);

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(dto.Password))
        {
            throw new ArgumentException(
                "Email and password are required.");
        }

        var user = await _userRepository.GetByEmailAsync(email);

        if (user is null ||
            user.IsDeleted ||
            !user.IsActive)
        {
            throw new UnauthorizedAccessException(
                "Invalid email or password.");
        }

        // Check whether the account is currently locked.
        if (user.LockoutEnd.HasValue &&
            user.LockoutEnd.Value > DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException(
                "Invalid email or password.");
        }

        bool passwordIsValid;

        try
        {
            passwordIsValid = BCrypt.Net.BCrypt.Verify(
                dto.Password,
                user.PasswordHash);
        }
        catch (SaltParseException)
        {
            passwordIsValid = false;
        }

        // Record failed login attempt.
        if (!passwordIsValid)
        {
            await _userRepository.RecordFailedLoginAttemptAsync(
                user.UserId,
                MaxFailedLoginAttempts,
                LockoutDurationMinutes);

            throw new UnauthorizedAccessException(
                "Invalid email or password.");
        }

        // Reset failed attempts after successful password verification.
        // If another request locked the account concurrently,
        // this operation returns false and no token is issued.
        var attemptsReset = await _userRepository.ResetLoginAttemptsAsync(
            user.UserId);

        if (!attemptsReset)
        {
            throw new UnauthorizedAccessException(
                "Invalid email or password.");
        }

        return CreateAuthResponse(user);
    }

    private AuthResponseDto CreateAuthResponse(User user)
    {
        var token = _jwtTokenGenerator.GenerateToken(
            user,
            out var expiresAt);

        return new AuthResponseDto
        {
            UserId = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role,
            Token = token,
            ExpiresAt = expiresAt
        };
    }

    private static string NormalizeEmail(string? email)
    {
        return email?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private static void ValidateFullName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName) ||
            fullName.Length < 3 ||
            fullName.Length > 150)
        {
            throw new ArgumentException(
                "Full name must be between 3 and 150 characters.");
        }
    }

    private static void ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 256)
        {
            throw new ArgumentException("Invalid email address.");
        }

        try
        {
            var address = new MailAddress(email);

            if (!address.Address.Equals(
                email,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Invalid email address.");
            }
        }
        catch (FormatException)
        {
            throw new ArgumentException("Invalid email address.");
        }
    }

    private static void ValidatePassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) ||
            password.Length < 8)
        {
            throw new ArgumentException(
                "Password must contain at least 8 characters.");
        }

        if (Encoding.UTF8.GetByteCount(password) > 72)
        {
            throw new ArgumentException(
                "Password cannot exceed 72 UTF-8 bytes.");
        }
    }
}
