using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ANpay.Api.Models;

public class ExchangeRate
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(10)]
    public string FromCurrency { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    public string ToCurrency { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "decimal(18,6)")]
    public decimal BuyRate { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,6)")]
    public decimal SellRate { get; set; }

    public decimal Spread => SellRate - BuyRate;

    public bool IsActive { get; set; } = true;

    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
