using System.ComponentModel.DataAnnotations;

namespace ANpay.Api.DTOs;

public class BeneficiaryDto
{
    public Guid Id { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public Guid WalletId { get; set; }
    public string WalletName { get; set; } = string.Empty;
    public string WalletCurrency { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateBeneficiaryDto
{
    [Required(ErrorMessage = "Nickname is required")]
    [MaxLength(100)]
    public string Nickname { get; set; } = string.Empty;

    [Required]
    public Guid WalletId { get; set; }

    [EmailAddress(ErrorMessage = "Invalid email address")]
    public string? Email { get; set; }
}

public class UpdateProfileDto
{
    [Required(ErrorMessage = "First name is required")]
    [MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required")]
    [MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Invalid phone number")]
    public string PhoneNumber { get; set; } = string.Empty;
}

public class ChangePasswordDto
{
    [Required(ErrorMessage = "Current password is required")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
    public string NewPassword { get; set; } = string.Empty;
}
