using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ANpay.Api.Models;

namespace ANpay.Api.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Wallet> Wallets { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<Branch> Branches { get; set; }
    public DbSet<Beneficiary> Beneficiaries { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Wallet>(entity =>
        {
            entity.HasOne(w => w.User)
                .WithMany(u => u.Wallets)
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(w => w.UserId);
            entity.HasIndex(w => new { w.UserId, w.Currency });
            entity.Property(w => w.RowVersion).IsRowVersion();
        });

        builder.Entity<Transaction>(entity =>
        {
            entity.HasOne(t => t.Wallet)
                .WithMany(w => w.Transactions)
                .HasForeignKey(t => t.WalletId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(t => t.DestinationWallet)
                .WithMany()
                .HasForeignKey(t => t.DestinationWalletId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(t => t.WalletId);
            entity.HasIndex(t => t.CreatedAt);
            entity.HasIndex(t => t.ReferenceNumber);
        });

        builder.Entity<Beneficiary>(entity =>
        {
            entity.HasOne(b => b.User)
                .WithMany()
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(b => b.Wallet)
                .WithMany()
                .HasForeignKey(b => b.WalletId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(b => b.UserId);
        });
    }
}
