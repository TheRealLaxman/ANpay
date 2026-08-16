using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class WhiteLabelTenant
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string TenantCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string ContactEmail { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ContactPhone { get; set; }

    [MaxLength(200)]
    public string? LogoUrl { get; set; }

    [MaxLength(200)]
    public string? PrimaryColor { get; set; } = "#007bff";

    [MaxLength(200)]
    public string? SecondaryColor { get; set; } = "#6c757d";

    [MaxLength(500)]
    public string? CustomDomain { get; set; }

    [MaxLength(500)]
    public string? ApiKey { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TransactionFeePercentage { get; set; } = 1.5m;

    public WhiteLabelStatus Status { get; set; } = WhiteLabelStatus.Active;

    public int MaxUsers { get; set; } = 1000;

    public int CurrentUsers { get; set; } = 0;

    public WhiteLabelPlan Plan { get; set; } = WhiteLabelPlan.Basic;

    [MaxLength(500)]
    public string? WebhookUrl { get; set; }

    [MaxLength(100)]
    public string? WebhookSecret { get; set; }

    public bool EnableNotifications { get; set; } = true;

    public bool EnableKyc { get; set; } = true;

    public bool EnableCrypto { get; set; } = false;

    public bool EnableMerchants { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastActiveAt { get; set; }

    public ICollection<TenantUser> Users { get; set; } = new List<TenantUser>();
}

public enum WhiteLabelStatus
{
    Active = 0,
    Suspended = 1,
    Trial = 2,
    Expired = 3
}

public enum WhiteLabelPlan
{
    Basic = 0,
    Professional = 1,
    Enterprise = 2,
    Custom = 3
}
