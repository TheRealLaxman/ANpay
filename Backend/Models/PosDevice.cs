using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class PosDevice
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    public string DeviceSerial { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string DeviceModel { get; set; } = string.Empty;

    [Required]
    public Guid MerchantId { get; set; }

    [ForeignKey("MerchantId")]
    public Merchant Merchant { get; set; } = null!;

    [MaxLength(100)]
    public string? AssignedUserId { get; set; }

    [ForeignKey("AssignedUserId")]
    public ApplicationUser? AssignedUser { get; set; }

    public PosDeviceStatus Status { get; set; } = PosDeviceStatus.Inactive;

    public bool SupportsNfc { get; set; } = true;

    public bool SupportsChip { get; set; } = true;

    public bool SupportsSwipe { get; set; } = true;

    public bool SupportsTapToPay { get; set; } = false;

    [MaxLength(100)]
    public string? FirmwareVersion { get; set; }

    [MaxLength(100)]
    public string? PublicKey { get; set; }

    public DateTime? LastSyncAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PosTransaction> Transactions { get; set; } = new List<PosTransaction>();
}

public enum PosDeviceStatus
{
    Inactive = 0,
    Active = 1,
    Suspended = 2,
    Offline = 3,
    Error = 4
}
