using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class BillProvider
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public BillCategory Category { get; set; }

    [Required]
    [MaxLength(10)]
    public string Code { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal MinimumAmount { get; set; } = 100;

    [Column(TypeName = "decimal(18,2)")]
    public decimal MaximumAmount { get; set; } = 500000;

    [Column(TypeName = "decimal(18,2)")]
    public decimal FixedFee { get; set; } = 0;

    [Column(TypeName = "decimal(5,2)")]
    public decimal PercentageFee { get; set; } = 0;

    [MaxLength(3)]
    public string Currency { get; set; } = "NGN";

    public bool IsActive { get; set; } = true;

    public bool RequiresBillerCode { get; set; } = true;

    [MaxLength(500)]
    public string? ApiEndpoint { get; set; }

    [MaxLength(100)]
    public string? ApiKey { get; set; }

    [MaxLength(200)]
    public string? LogoUrl { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
