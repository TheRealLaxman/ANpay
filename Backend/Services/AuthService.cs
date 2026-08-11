using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        ApplicationDbContext context,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _configuration = configuration;
        _context = context;
        _logger = logger;
    }

    public async Task SeedRolesAndAdminAsync()
    {
        string[] roles = { "Customer", "Official", "BranchAdmin", "SuperAdmin" };

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

            var adminPassword = _configuration["AdminSettings:InitialPassword"] ?? "Admin@123";
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

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
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

        var wallet = new Wallet
        {
            UserId = user.Id,
            WalletName = "Main Wallet",
            Currency = "USD",
            Balance = 0
        };
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User registered successfully: {Email}", dto.Email);

        var token = GenerateJwtToken(user);

        return new AuthResponseDto
        {
            Success = true,
            Token = token,
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            Role = user.Role.ToString(),
            FirstName = user.FirstName,
            LastName = user.LastName,
            Message = "Registration successful"
        };
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
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

        var isValid = await _userManager.CheckPasswordAsync(user, dto.Password);
        if (!isValid)
        {
            _logger.LogWarning("Login failed - invalid password for: {Email}", dto.Email);
            return new AuthResponseDto
            {
                Success = false,
                Message = "Invalid email or password"
            };
        }

        _logger.LogInformation("User logged in successfully: {Email}", dto.Email);

        var token = GenerateJwtToken(user);

        return new AuthResponseDto
        {
            Success = true,
            Token = token,
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            Role = user.Role.ToString(),
            FirstName = user.FirstName,
            LastName = user.LastName,
            Message = "Login successful"
        };
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

    private string GenerateJwtToken(ApplicationUser user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Secret"]!));
        var expires = DateTime.UtcNow.AddDays(Convert.ToDouble(jwtSettings["ExpirationInDays"]));

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
}
