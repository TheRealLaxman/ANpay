using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class CashBalance
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid BranchId { get; set; }

    [ForeignKey("BranchId")]
    public Branch Branch { get; set; } = null!;

    public string? EmployeeId { get; set; }

    [ForeignKey("EmployeeId")]
    public ApplicationUser? Employee { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OpeningBalance { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalDeposits { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalWithdrawals { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Adjustments { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ExpectedClosing => OpeningBalance + TotalDeposits - TotalWithdrawals + Adjustments;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualClosing { get; set; } = 0;

    public decimal Difference => ActualClosing - ExpectedClosing;

    public DateTime Date { get; set; } = DateTime.UtcNow.Date;

    public bool IsReconciled { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
