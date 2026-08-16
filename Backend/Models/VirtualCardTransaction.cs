using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class VirtualCardTransaction
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid VirtualCardId { get; set; }

    [ForeignKey("VirtualCardId")]
    public VirtualCard VirtualCard { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string MerchantName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? MerchantCategory { get; set; }

    [Required]
    public VirtualCardTransactionType Type { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OriginalAmount { get; set; }

    [Required]
    [MaxLength(3)]
    public string OriginalCurrency { get; set; } = "USD";

    [Required]
    public VirtualCardTransactionStatus Status { get; set; }

    [MaxLength(100)]
    public string? AuthorizationCode { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    public bool IsFraudulent { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum VirtualCardTransactionType
{
    Purchase = 0,
    Refund = 1,
    ATMWithdrawal = 2,
    Fee = 3,
    Cashback = 4
}

public enum VirtualCardTransactionStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
    Declined = 3,
    Reversed = 4
}
