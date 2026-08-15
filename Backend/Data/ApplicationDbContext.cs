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
    public DbSet<CryptoWallet> CryptoWallets { get; set; }
    public DbSet<CryptoTransaction> CryptoTransactions { get; set; }
    public DbSet<CryptoNetworkConfig> CryptoNetworkConfigs { get; set; }
    public DbSet<Merchant> Merchants { get; set; }
    public DbSet<MerchantPayment> MerchantPayments { get; set; }
    public DbSet<MerchantSettlement> MerchantSettlements { get; set; }
    public DbSet<QrCode> QrCodes { get; set; }
    public DbSet<PaymentLink> PaymentLinks { get; set; }
    public DbSet<Dispute> Disputes { get; set; }
    public DbSet<DisputeMessage> DisputeMessages { get; set; }
    public DbSet<FraudAlert> FraudAlerts { get; set; }
    public DbSet<RiskScore> RiskScores { get; set; }
    public DbSet<SystemSetting> SystemSettings { get; set; }
    public DbSet<LoginHistory> LoginHistories { get; set; }
    public DbSet<TrustedDevice> TrustedDevices { get; set; }
    public DbSet<IdempotencyKey> IdempotencyKeys { get; set; }
    public DbSet<ReconciliationRecord> ReconciliationRecords { get; set; }
    public DbSet<ReconciliationTransaction> ReconciliationTransactions { get; set; }
    public DbSet<ScheduledTransfer> ScheduledTransfers { get; set; }

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
            entity.HasIndex(t => t.Channel);
            entity.HasIndex(t => t.BranchId);
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
                .OnDelete(DeleteBehavior.Restrict);

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

        // CryptoWallet
        builder.Entity<CryptoWallet>(entity =>
        {
            entity.HasOne(cw => cw.User)
                .WithMany()
                .HasForeignKey(cw => cw.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(cw => cw.UserId);
            entity.HasIndex(cw => new { cw.UserId, cw.Asset, cw.Network }).IsUnique();
        });

        // CryptoTransaction
        builder.Entity<CryptoTransaction>(entity =>
        {
            entity.HasOne(ct => ct.CryptoWallet)
                .WithMany(cw => cw.Transactions)
                .HasForeignKey(ct => ct.CryptoWalletId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(ct => ct.CryptoWalletId);
            entity.HasIndex(ct => ct.TxHash);
            entity.HasIndex(ct => ct.CreatedAt);
        });

        // Merchant
        builder.Entity<Merchant>(entity =>
        {
            entity.HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(m => m.UserId).IsUnique();
        });

        // MerchantPayment
        builder.Entity<MerchantPayment>(entity =>
        {
            entity.HasOne(mp => mp.Merchant)
                .WithMany(m => m.Payments)
                .HasForeignKey(mp => mp.MerchantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(mp => mp.MerchantId);
            entity.HasIndex(mp => mp.OrderReference);
            entity.HasIndex(mp => mp.CreatedAt);
        });

        // MerchantSettlement
        builder.Entity<MerchantSettlement>(entity =>
        {
            entity.HasOne(ms => ms.Merchant)
                .WithMany(m => m.Settlements)
                .HasForeignKey(ms => ms.MerchantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(ms => ms.MerchantId);
        });

        // QrCode
        builder.Entity<QrCode>(entity =>
        {
            entity.HasOne(q => q.CreatedBy)
                .WithMany()
                .HasForeignKey(q => q.CreatedById)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(q => q.Code).IsUnique();
            entity.HasIndex(q => q.CreatedById);
        });

        // PaymentLink
        builder.Entity<PaymentLink>(entity =>
        {
            entity.HasOne(pl => pl.CreatedBy)
                .WithMany()
                .HasForeignKey(pl => pl.CreatedById)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(pl => pl.LinkUrl);
        });

        // Dispute
        builder.Entity<Dispute>(entity =>
        {
            entity.HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Transaction)
                .WithMany()
                .HasForeignKey(d => d.TransactionId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.AssignedTo)
                .WithMany()
                .HasForeignKey(d => d.AssignedToId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(d => d.UserId);
            entity.HasIndex(d => d.Status);
            entity.HasIndex(d => d.CreatedAt);
        });

        // DisputeMessage
        builder.Entity<DisputeMessage>(entity =>
        {
            entity.HasOne(dm => dm.Dispute)
                .WithMany(d => d.Messages)
                .HasForeignKey(dm => dm.DisputeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(dm => dm.Sender)
                .WithMany()
                .HasForeignKey(dm => dm.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // FraudAlert
        builder.Entity<FraudAlert>(entity =>
        {
            entity.HasOne(fa => fa.User)
                .WithMany()
                .HasForeignKey(fa => fa.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(fa => fa.AssignedTo)
                .WithMany()
                .HasForeignKey(fa => fa.AssignedToId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(fa => fa.UserId);
            entity.HasIndex(fa => fa.Status);
            entity.HasIndex(fa => fa.CreatedAt);
        });

        // RiskScore
        builder.Entity<RiskScore>(entity =>
        {
            entity.HasIndex(rs => new { rs.EntityType, rs.EntityId });
            entity.HasIndex(rs => rs.CalculatedAt);
        });

        // SystemSetting
        builder.Entity<SystemSetting>(entity =>
        {
            entity.HasIndex(ss => ss.Key).IsUnique();
        });

        // LoginHistory
        builder.Entity<LoginHistory>(entity =>
        {
            entity.HasOne(lh => lh.User)
                .WithMany()
                .HasForeignKey(lh => lh.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(lh => lh.UserId);
            entity.HasIndex(lh => lh.Timestamp);
        });

        // TrustedDevice
        builder.Entity<TrustedDevice>(entity =>
        {
            entity.HasOne(td => td.User)
                .WithMany()
                .HasForeignKey(td => td.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(td => td.UserId);
        });

        // IdempotencyKey
        builder.Entity<IdempotencyKey>(entity =>
        {
            entity.HasKey(ik => ik.Key);
            entity.HasIndex(ik => new { ik.Key, ik.UserId }).IsUnique();
            entity.HasIndex(ik => ik.ExpiresAt);
            entity.HasIndex(ik => ik.UserId);
        });

        // ReconciliationRecord
        builder.Entity<ReconciliationRecord>(entity =>
        {
            entity.HasIndex(r => r.Type);
            entity.HasIndex(r => r.Status);
            entity.HasIndex(r => r.CreatedAt);
        });

        // ReconciliationTransaction
        builder.Entity<ReconciliationTransaction>(entity =>
        {
            entity.HasOne(rt => rt.ReconciliationRecord)
                .WithMany(r => r.ReconciliationTransactions)
                .HasForeignKey(rt => rt.ReconciliationRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(rt => rt.ReconciliationRecordId);
            entity.HasIndex(rt => rt.TransactionId);
        });

        // ScheduledTransfer
        builder.Entity<ScheduledTransfer>(entity =>
        {
            entity.HasOne(st => st.User)
                .WithMany()
                .HasForeignKey(st => st.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(st => st.SourceWallet)
                .WithMany()
                .HasForeignKey(st => st.SourceWalletId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(st => st.DestinationWallet)
                .WithMany()
                .HasForeignKey(st => st.DestinationWalletId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(st => st.UserId);
            entity.HasIndex(st => st.NextExecutionDate);
            entity.HasIndex(st => st.Status);
        });
    }
}
