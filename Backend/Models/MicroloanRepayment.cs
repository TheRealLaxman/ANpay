using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class MicroloanRepayment
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid MicroloanId { get; set; }

    [ForeignKey("MicroloanId")]
    public Microloan Microloan { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public DateTime DueDate { get; set; }

    public DateTime? PaidDate { get; set; }

    public MicroloanRepaymentStatus Status { get; set; } = MicroloanRepaymentStatus.Pending;

    [MaxLength(100)]
    public string? TransactionReference { get; set; }
}

public enum MicroloanRepaymentStatus
{
    Pending = 0,
    Paid = 1,
    Overdue = 2,
    PartiallyPaid = 3
}
