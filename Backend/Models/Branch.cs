using System.ComponentModel.DataAnnotations;

namespace ANpay.Api.Models;

public enum BranchStatus
{
    Active = 0,
    Suspended = 1,
    Closed = 2,
    UnderMaintenance = 3
}

public class Branch
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    public BranchStatus Status { get; set; } = BranchStatus.Active;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ApplicationUser> Employees { get; set; } = new List<ApplicationUser>();
    public ICollection<EmployeeProfile> EmployeeProfiles { get; set; } = new List<EmployeeProfile>();
    public ICollection<CashBalance> CashBalances { get; set; } = new List<CashBalance>();
}
