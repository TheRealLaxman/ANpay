using Microsoft.AspNetCore.Identity;

namespace ANpay.Api.Models;

public enum AppUserRole
{
    Customer = 0,
    Official = 1,
    BranchAdmin = 2,
    SuperAdmin = 3
}

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public AppUserRole Role { get; set; } = AppUserRole.Customer;
    public Guid? BranchId { get; set; }
    public ICollection<Wallet> Wallets { get; set; } = new List<Wallet>();
}
