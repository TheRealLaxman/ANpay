using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ANpay.Api.Data;
using ANpay.Api.DTOs;
using ANpay.Api.Exceptions;
using ANpay.Api.Models;

namespace ANpay.Api.Services;

public class AuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasher<ApplicationUser> _pinHasher;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        ApplicationDbContext context,
        IPasswordHasher<ApplicationUser> pinHasher,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _configuration = configuration;
        _context = context;
        _pinHasher = pinHasher;
        _logger = logger;
    }

    public async Task SeedRolesAndAdminAsync()
    {
        string[] roles = { "Customer", "Official", "BranchAdmin", "MainBranchAdmin", "SuperAdmin" };

        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
                await _roleManager.CreateAsync(new IdentityRole(role));
        }

        var adminEmail = "admin@anpay.com";
        var adminUser = await _userManager.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "Super",
                LastName = "Admin",
                Role = AppUserRole.SuperAdmin,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            var adminPassword = _configuration["AdminSettings:InitialPassword"]
                ?? throw new InvalidOperationException("AdminSettings:InitialPassword must be configured for admin seeding");
            var result = await _userManager.CreateAsync(adminUser, adminPassword);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(adminUser, "SuperAdmin");
                _logger.LogInformation("SuperAdmin account seeded successfully");
            }
            else
            {
                _logger.LogError("Failed to seed SuperAdmin: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto, string? ipAddress = null)
    {
        _logger.LogInformation("Registration attempt for email: {Email}", dto.Email);

        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        if (existingUser != null)
        {
            _logger.LogWarning("Registration failed - email already registered: {Email}", dto.Email);
            return new AuthResponseDto
            {
                Success = false,
                Message = "Email already registered"
            };
        }

        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            PhoneNumber = dto.PhoneNumber,
            Role = AppUserRole.Customer,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Registration failed for {Email}: {Errors}",
                dto.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
            return new AuthResponseDto
            {
                Success = false,
                Message = string.Join(", ", result.Errors.Select(e => e.Description))
            };
        }

        await _userManager.AddToRoleAsync(user, "Customer");

        await using var dbTransaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var wallet = new Wallet
            {
                UserId = user.Id,
                WalletName = "Main Wallet",
                Currency = "USD",
                Balance = 0
            };
            _context.Wallets.Add(wallet);
            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();
        }
        catch
        {
            await dbTransaction.RollbackAsync();
            await _userManager.DeleteAsync(user);
            throw;
        }

        _logger.LogInformation("User registered successfully: {Email}", dto.Email);

        var jwtToken = GenerateJwtToken(user);
        var refreshToken = await GenerateRefreshTokenAsync(user.Id, ipAddress);

        return new AuthResponseDto
        {
            Success = true,
            Token = jwtToken,
            RefreshToken = refreshToken.Token,
            RefreshTokenExpiresAt = refreshToken.ExpiresAt,
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            Role = user.Role.ToString(),
            FirstName = user.FirstName,
            LastName = user.LastName,
            Message = "Registration successful"
        };
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto, string? ipAddress = null, string? userAgent = null)
    {
        _logger.LogInformation("Login attempt for email: {Email}", dto.Email);

        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null)
        {
            _logger.LogWarning("Login failed - invalid email: {Email}", dto.Email);
            return new AuthResponseDto
            {
                Success = false,
                Message = "Invalid email or password"
            };
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login failed - account deactivated: {Email}", dto.Email);
            return new AuthResponseDto
            {
                Success = false,
                Message = "Account has been deactivated. Please contact support."
            };
        }

        var isValid = await _userManager.CheckPasswordAsync(user, dto.Password);
        if (!isValid)
        {
            _logger.LogWarning("Login failed - invalid password for: {Email}", dto.Email);
            await RecordLoginAsync(user.Id, ipAddress, userAgent, false, "Invalid password");
            return new AuthResponseDto
            {
                Success = false,
                Message = "Invalid email or password"
            };
        }

        _logger.LogInformation("User logged in successfully: {Email}", dto.Email);
        await RecordLoginAsync(user.Id, ipAddress, userAgent, true);

        var jwtToken = GenerateJwtToken(user);
        var refreshToken = await GenerateRefreshTokenAsync(user.Id, ipAddress);

        return new AuthResponseDto
        {
            Success = true,
            Token = jwtToken,
            RefreshToken = refreshToken.Token,
            RefreshTokenExpiresAt = refreshToken.ExpiresAt,
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            Role = user.Role.ToString(),
            FirstName = user.FirstName,
            LastName = user.LastName,
            Message = "Login successful"
        };
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(string refreshTokenValue, string? ipAddress = null)
    {
        var refreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == refreshTokenValue);

        if (refreshToken == null)
        {
            _logger.LogWarning("Refresh token not found");
            return new AuthResponseDto { Success = false, Message = "Invalid refresh token" };
        }

        if (refreshToken.IsExpired)
        {
            _logger.LogWarning("Refresh token expired for user {UserId}", refreshToken.UserId);
            return new AuthResponseDto { Success = false, Message = "Refresh token expired" };
        }

        if (refreshToken.IsRevoked)
        {
            _logger.LogWarning("Refresh token revoked for user {UserId}", refreshToken.UserId);
            return new AuthResponseDto { Success = false, Message = "Refresh token revoked" };
        }

        var user = await _userManager.FindByIdAsync(refreshToken.UserId);
        if (user == null || !user.IsActive)
        {
            return new AuthResponseDto { Success = false, Message = "User not found or inactive" };
        }

        // Revoke the old refresh token
        refreshToken.RevokedAt = DateTime.UtcNow;
        refreshToken.ReplacedByToken = "replaced";
        await _context.SaveChangesAsync();

        // Generate new tokens
        var newJwtToken = GenerateJwtToken(user);
        var newRefreshToken = await GenerateRefreshTokenAsync(user.Id, ipAddress);

        _logger.LogInformation("Token refreshed for user {UserId}", user.Id);

        return new AuthResponseDto
        {
            Success = true,
            Token = newJwtToken,
            RefreshToken = newRefreshToken.Token,
            RefreshTokenExpiresAt = newRefreshToken.ExpiresAt,
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            Role = user.Role.ToString(),
            FirstName = user.FirstName,
            LastName = user.LastName,
            Message = "Token refreshed"
        };
    }

    public async Task RevokeRefreshTokenAsync(string refreshTokenValue, string userId)
    {
        var refreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == refreshTokenValue && rt.UserId == userId);

        if (refreshToken != null && !refreshToken.IsRevoked)
        {
            refreshToken.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Refresh token revoked for user {UserId}", userId);
        }
    }

    private async Task<RefreshToken> GenerateRefreshTokenAsync(string userId, string? ipAddress)
    {
        var refreshToken = new RefreshToken
        {
            UserId = userId,
            Token = GenerateRandomToken(),
            JwtId = Guid.NewGuid().ToString(),
            ExpiresAt = DateTime.UtcNow.AddDays(Convert.ToDouble(_configuration["JwtSettings:RefreshTokenExpirationInDays"] ?? "7")),
            CreatedByIp = ipAddress
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        return refreshToken;
    }

    private static string GenerateRandomToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public async Task<UserProfileDto> GetProfileAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("Profile not found for userId: {UserId}", userId);
            throw new NotFoundException("User not found");
        }

        return new UserProfileDto
        {
            UserId = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber ?? string.Empty,
            BranchId = user.BranchId?.ToString(),
            IsTransactionPinSet = user.IsTransactionPinSet,
            CreatedAt = user.CreatedAt
        };
    }

    public async Task<UserProfileDto> UpdateProfileAsync(string userId, UpdateProfileDto dto)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new NotFoundException("User not found");

        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;
        user.PhoneNumber = dto.PhoneNumber;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new ValidationException(string.Join(", ", result.Errors.Select(e => e.Description)));

        _logger.LogInformation("Profile updated for user {UserId}", userId);

        return new UserProfileDto
        {
            UserId = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber ?? string.Empty,
            CreatedAt = user.CreatedAt
        };
    }

    public async Task ChangePasswordAsync(string userId, ChangePasswordDto dto)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new NotFoundException("User not found");

        var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
        if (!result.Succeeded)
            throw new ValidationException(string.Join(", ", result.Errors.Select(e => e.Description)));

        _logger.LogInformation("Password changed for user {UserId}", userId);
    }

    public async Task SetTransactionPinAsync(string userId, string pin)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("User not found");

        if (string.IsNullOrEmpty(pin) || pin.Length != 6 || !pin.All(char.IsDigit))
            throw new ValidationException("PIN must be exactly 6 digits");

        // Reject weak PINs
        var weakPins = new[] { "000000", "111111", "222222", "333333", "444444", "555555", "666666", "777777", "888888", "999999", "123456", "654321" };
        if (weakPins.Contains(pin))
            throw new ValidationException("PIN is too weak. Please choose a different PIN.");

        user.TransactionPinHash = _pinHasher.HashPassword(user, pin);
        user.IsTransactionPinSet = true;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new ValidationException(string.Join(", ", result.Errors.Select(e => e.Description)));

        _logger.LogInformation("Transaction PIN set for user {UserId}", userId);
    }

    public async Task<bool> VerifyTransactionPinAsync(string userId, string pin)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("User not found");

        if (!user.IsTransactionPinSet || string.IsNullOrEmpty(user.TransactionPinHash))
            throw new ValidationException("Transaction PIN not set. Please set a PIN first.");

        return _pinHasher.VerifyHashedPassword(user, user.TransactionPinHash, pin) == PasswordVerificationResult.Success;
    }

    public string GetAppUrl()
    {
        return _configuration["AppUrl"] ?? "http://localhost:5069";
    }

    public async Task<string> GeneratePasswordResetTokenAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email)
            ?? throw new NotFoundException("No account found with this email");

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        return token;
    }

    public async Task ResetPasswordAsync(string email, string token, string newPassword)
    {
        var user = await _userManager.FindByEmailAsync(email)
            ?? throw new NotFoundException("No account found with this email");

        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
            throw new ValidationException(string.Join(", ", result.Errors.Select(e => e.Description)));

        _logger.LogInformation("Password reset for user {UserId}", user.Id);
    }

    private string GenerateJwtToken(ApplicationUser user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secret = jwtSettings["Secret"]
            ?? throw new InvalidOperationException("JwtSettings:Secret must be configured");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var expires = DateTime.UtcNow.AddMinutes(Convert.ToDouble(jwtSettings["ExpirationInMinutes"] ?? "15"));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.GivenName, user.FirstName),
            new(ClaimTypes.Surname, user.LastName),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task RecordLoginAsync(string userId, string? ipAddress, string? userAgent, bool isSuccess, string? failureReason = null)
    {
        try
        {
            var loginHistory = new LoginHistory
            {
                UserId = userId,
                IPAddress = ipAddress,
                UserAgent = userAgent,
                IsSuccess = isSuccess,
                FailureReason = failureReason
            };
            _context.LoginHistories.Add(loginHistory);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record login history for user {UserId}", userId);
        }
    }

    public async Task<List<LoginHistory>> GetLoginHistoryAsync(string userId, int limit = 20)
    {
        return await _context.LoginHistories
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<TrustedDevice>> GetActiveDevicesAsync(string userId)
    {
        return await _context.TrustedDevices
            .Where(d => d.UserId == userId && d.IsActive)
            .OrderByDescending(d => d.LastUsedAt)
            .ToListAsync();
    }

    public async Task RevokeDeviceAsync(string userId, Guid deviceId)
    {
        var device = await _context.TrustedDevices
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == userId)
            ?? throw new NotFoundException("Device not found");

        device.IsActive = false;
        await _context.SaveChangesAsync();
    }

    public async Task<string> GenerateOtpAsync(string userId)
    {
        var otp = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        var setting = new SystemSetting
        {
            Key = $"OTP:{userId}",
            Value = otp,
            Category = "OTP",
            Description = $"OTP for user {userId}, expires at {DateTime.UtcNow.AddMinutes(5):O}"
        };

        var existing = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == $"OTP:{userId}");
        if (existing != null)
        {
            existing.Value = otp;
            existing.Description = setting.Description;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _context.SystemSettings.Add(setting);
        }
        await _context.SaveChangesAsync();

        _logger.LogInformation("OTP generated for user {UserId}", userId);
        return otp;
    }

    public async Task<bool> VerifyOtpAsync(string userId, string otp)
    {
        var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == $"OTP:{userId}");
        if (setting == null || setting.Value != otp) return false;

        var desc = setting.Description ?? "";
        if (desc.Contains("expires at"))
        {
            var expiryStr = desc.Split("expires at ").LastOrDefault();
            if (DateTime.TryParse(expiryStr, out var expiry) && DateTime.UtcNow > expiry)
                return false;
        }

        _context.SystemSettings.Remove(setting);
        await _context.SaveChangesAsync();
        return true;
    }
}
