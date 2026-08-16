using System.ComponentModel.DataAnnotations;

namespace ANpay.Api.DTOs;

public class RegisterDto
{
    [Required(ErrorMessage = "First name is required")]
    [MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required")]
    [MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
    public string Password { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Invalid phone number")]
    public string PhoneNumber { get; set; } = string.Empty;
}

public class LoginDto
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponseDto
{
    public bool Success { get; set; }
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAt { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class RefreshTokenDto
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public class UserProfileDto
{
    public string UserId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? BranchId { get; set; }
    public bool IsTransactionPinSet { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SetPinDto
{
    [Required]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "PIN must be exactly 6 digits")]
    public string Pin { get; set; } = string.Empty;
}

public class VerifyPinDto
{
    [Required]
    public string Pin { get; set; } = string.Empty;
}

public class ForgotPasswordDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string NewPassword { get; set; } = string.Empty;
}

public class FeeQuoteDto
{
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Currency { get; set; } = "NGN";
}

public class FeeQuoteResultDto
{
    public decimal Amount { get; set; }
    public decimal Fee { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = string.Empty;
}

public class SpendingAnalyticsDto
{
    public decimal TotalSpent { get; set; }
    public decimal TotalReceived { get; set; }
    public decimal AverageTransaction { get; set; }
    public int TransactionCount { get; set; }
    public List<CategoryBreakdown> ByCategory { get; set; } = new();
    public List<MonthlySpending> MonthlyTrend { get; set; } = new();
}

public class CategoryBreakdown
{
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public class MonthlySpending
{
    public string Month { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class TransactionLimitsDto
{
    public List<LimitItem> Limits { get; set; } = new();
}

public class LimitItem
{
    public string Type { get; set; } = string.Empty;
    public decimal LimitAmount { get; set; }
    public decimal Used { get; set; }
    public decimal Remaining { get; set; }
    public string Currency { get; set; } = string.Empty;
}
