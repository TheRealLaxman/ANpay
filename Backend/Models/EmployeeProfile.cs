using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public enum EmployeeSubRole
{
    Teller = 0,
    CustomerService = 1,
    Operations = 2,
    Manager = 3
}

public class EmployeeProfile
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey("UserId")]
    public ApplicationUser User { get; set; } = null!;

    [Required]
    public Guid BranchId { get; set; }

    [ForeignKey("BranchId")]
    public Branch Branch { get; set; } = null!;

    [Required]
    public EmployeeSubRole SubRole { get; set; }

    [MaxLength(100)]
    public string EmployeeCode { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "decimal(18,2)")]
    public decimal CashBalance { get; set; } = 0;
}
