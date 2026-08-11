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
    public DbSet<RolePermission> RolePermissions { get; set; }
    public DbSet<EmployeeProfile> EmployeeProfiles { get; set; }
    public DbSet<KycProfile> KycProfiles { get; set; }
    public DbSet<KycDocument> KycDocuments { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<LedgerAccount> LedgerAccounts { get; set; }
    public DbSet<LedgerEntry> LedgerEntries { get; set; }
    public DbSet<Fee> Fees { get; set; }
    public DbSet<TransactionLimit> TransactionLimits { get; set; }
    public DbSet<ApprovalRequest> ApprovalRequests { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<SupportTicket> SupportTickets { get; set; }
    public DbSet<TicketMessage> TicketMessages { get; set; }
    public DbSet<ExchangeRate> ExchangeRates { get; set; }
    public DbSet<CashBalance> CashBalances { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Wallet
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

        // Transaction
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

        // Beneficiary
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

        // RolePermission
        builder.Entity<RolePermission>(entity =>
        {
            entity.HasIndex(rp => new { rp.RoleName, rp.Permission }).IsUnique();
        });

        // EmployeeProfile
        builder.Entity<EmployeeProfile>(entity =>
        {
            entity.HasOne(ep => ep.User)
                .WithMany()
                .HasForeignKey(ep => ep.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ep => ep.Branch)
                .WithMany(b => b.EmployeeProfiles)
                .HasForeignKey(ep => ep.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(ep => ep.UserId).IsUnique();
            entity.HasIndex(ep => ep.BranchId);
        });

        // Branch
        builder.Entity<Branch>(entity =>
        {
            entity.HasIndex(b => b.Name).IsUnique();
        });

        // KycProfile
        builder.Entity<KycProfile>(entity =>
        {
            entity.HasOne(kp => kp.User)
                .WithOne(u => u.KycProfile)
                .HasForeignKey<KycProfile>(kp => kp.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(kp => kp.UserId).IsUnique();
        });

        // KycDocument
        builder.Entity<KycDocument>(entity =>
        {
            entity.HasOne(kd => kd.KycProfile)
                .WithMany(kp => kp.Documents)
                .HasForeignKey(kd => kd.KycProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AuditLog
        builder.Entity<AuditLog>(entity =>
        {
            entity.HasOne(al => al.User)
                .WithMany()
                .HasForeignKey(al => al.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(al => al.UserId);
            entity.HasIndex(al => al.CreatedAt);
            entity.HasIndex(al => al.Action);
        });

        // LedgerEntry
        builder.Entity<LedgerEntry>(entity =>
        {
            entity.HasOne(le => le.Transaction)
                .WithMany()
                .HasForeignKey(le => le.TransactionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(le => le.DebitAccount)
                .WithMany(a => a.DebitEntries)
                .HasForeignKey(le => le.DebitAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(le => le.CreditAccount)
                .WithMany(a => a.CreditEntries)
                .HasForeignKey(le => le.CreditAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(le => le.TransactionId);
            entity.HasIndex(le => le.CreatedAt);
        });

        // LedgerAccount
        builder.Entity<LedgerAccount>(entity =>
        {
            entity.HasIndex(a => a.Code).IsUnique();
            entity.HasOne(a => a.ParentAccount)
                .WithMany()
                .HasForeignKey(a => a.ParentAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Fee
        builder.Entity<Fee>(entity =>
        {
            entity.HasIndex(f => new { f.AppliesTo, f.IsActive });
        });

        // TransactionLimit
        builder.Entity<TransactionLimit>(entity =>
        {
            entity.HasIndex(tl => new { tl.RoleName, tl.LimitType }).IsUnique();
        });

        // ApprovalRequest
        builder.Entity<ApprovalRequest>(entity =>
        {
            entity.HasOne(ar => ar.RequestedBy)
                .WithMany()
                .HasForeignKey(ar => ar.RequestedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ar => ar.ApprovedBy)
                .WithMany()
                .HasForeignKey(ar => ar.ApprovedById)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(ar => ar.Status);
            entity.HasIndex(ar => ar.RequestedById);
        });

        // Notification
        builder.Entity<Notification>(entity =>
        {
            entity.HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(n => new { n.UserId, n.IsRead });
            entity.HasIndex(n => n.CreatedAt);
        });

        // SupportTicket
        builder.Entity<SupportTicket>(entity =>
        {
            entity.HasOne(st => st.User)
                .WithMany()
                .HasForeignKey(st => st.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(st => st.AssignedTo)
                .WithMany()
                .HasForeignKey(st => st.AssignedToId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(st => st.UserId);
            entity.HasIndex(st => st.Status);
            entity.HasIndex(st => st.AssignedToId);
        });

        // TicketMessage
        builder.Entity<TicketMessage>(entity =>
        {
            entity.HasOne(tm => tm.Ticket)
                .WithMany(t => t.Messages)
                .HasForeignKey(tm => tm.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(tm => tm.Sender)
                .WithMany()
                .HasForeignKey(tm => tm.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // CashBalance
        builder.Entity<CashBalance>(entity =>
        {
            entity.HasOne(cb => cb.Branch)
                .WithMany(b => b.CashBalances)
                .HasForeignKey(cb => cb.BranchId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(cb => cb.Employee)
                .WithMany()
                .HasForeignKey(cb => cb.EmployeeId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(cb => new { cb.BranchId, cb.Date });
        });

        // ExchangeRate
        builder.Entity<ExchangeRate>(entity =>
        {
            entity.HasIndex(er => new { er.FromCurrency, er.ToCurrency }).IsUnique();
        });
    }
}
