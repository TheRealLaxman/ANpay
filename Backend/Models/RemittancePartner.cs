using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class RemittancePartner
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Country { get; set; } = string.Empty;

    [Required]
    public PartnerType Type { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal CommissionRate { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal MinimumAmount { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal MaximumAmount { get; set; } = 100000;

    [MaxLength(500)]
    public string? ApiEndpoint { get; set; }

    [MaxLength(100)]
    public string? ApiKey { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(200)]
    public string? LogoUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum PartnerType
{
    Bank = 0,
    MobileMoney = 1,
    PaymentProvider = 2,
    ExchangeBureau = 3
}