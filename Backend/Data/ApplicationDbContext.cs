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

    // P0: Virtual Cards
    public DbSet<VirtualCard> VirtualCards { get; set; }
    public DbSet<VirtualCardTransaction> VirtualCardTransactions { get; set; }

    // P0: Bill Payments
    public DbSet<BillPayment> BillPayments { get; set; }
    public DbSet<BillProvider> BillProviders { get; set; }

    // P1: Credit Scoring
    public DbSet<CreditScore> CreditScores { get; set; }
    public DbSet<CreditScoreFactor> CreditScoreFactors { get; set; }

    // P1: Cross-Border
    public DbSet<Remittance> Remittances { get; set; }
    public DbSet<RemittancePartner> RemittancePartners { get; set; }

    // P1: Loyalty & Rewards
    public DbSet<LoyaltyPoint> LoyaltyPoints { get; set; }
    public DbSet<LoyaltyTransaction> LoyaltyTransactions { get; set; }
    public DbSet<Referral> Referrals { get; set; }
    public DbSet<Cashback> Cashbacks { get; set; }

    // P2: BNPL
    public DbSet<BuyNowPayLater> BuyNowPayLaters { get; set; }
    public DbSet<BnplInstallment> BnplInstallments { get; set; }

    // P2: Open Banking
    public DbSet<OpenBankingAccount> OpenBankingAccounts { get; set; }
    public DbSet<ApiKey> ApiKeys { get; set; }
    public DbSet<Webhook> Webhooks { get; set; }
    public DbSet<WebhookDelivery> WebhookDeliveries { get; set; }

    // P2: POS
    public DbSet<PosDevice> PosDevices { get; set; }
    public DbSet<PosTransaction> PosTransactions { get; set; }

    // P3: White-Label
    public DbSet<WhiteLabelTenant> WhiteLabelTenants { get; set; }
    public DbSet<TenantUser> TenantUsers { get; set; }

    // P3: Microloans
    public DbSet<Microloan> Microloans { get; set; }
    public DbSet<MicroloanRepayment> MicroloanRepayments { get; set; }

    // P3: Insurance
    public DbSet<Insurance> Insurances { get; set; }
    public DbSet<InsuranceClaim> InsuranceClaims { get; set; }

    // P3: Investments
    public DbSet<Investment> Investments { get; set; }
    public DbSet<InvestmentTransaction> InvestmentTransactions { get; set; }
    public DbSet<SavingsGoal> SavingsGoals { get; set; }

    // AI Assistant
    public DbSet<AiChat> AiChats { get; set; }
    public DbSet<AiMessage> AiMessages { get; set; }
    public DbSet<AiTrainingData> AiTrainingData { get; set; }

    // Auth
    public DbSet<RefreshToken> RefreshTokens { get; set; }

    // WebAuthn
    public DbSet<WebAuthnCredential> WebAuthnCredentials { get; set; }
    public DbSet<WebAuthnChallenge> WebAuthnChallenges { get; set; }

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
                .OnDelete(DeleteBehavior.Restrict);

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

        // VirtualCard
        builder.Entity<VirtualCard>(entity =>
        {
            entity.HasOne(vc => vc.User)
                .WithMany()
                .HasForeignKey(vc => vc.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(vc => vc.Wallet)
                .WithMany()
                .HasForeignKey(vc => vc.WalletId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(vc => vc.UserId);
            entity.HasIndex(vc => vc.WalletId);
            entity.HasIndex(vc => vc.CardNumber);
            entity.HasIndex(vc => vc.Status);
        });

        // VirtualCardTransaction
        builder.Entity<VirtualCardTransaction>(entity =>
        {
            entity.HasOne(vct => vct.VirtualCard)
                .WithMany(vc => vc.Transactions)
                .HasForeignKey(vct => vct.VirtualCardId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(vct => vct.VirtualCardId);
            entity.HasIndex(vct => vct.CreatedAt);
        });

        // BillPayment
        builder.Entity<BillPayment>(entity =>
        {
            entity.HasOne(bp => bp.User)
                .WithMany()
                .HasForeignKey(bp => bp.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(bp => bp.Wallet)
                .WithMany()
                .HasForeignKey(bp => bp.WalletId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(bp => bp.UserId);
            entity.HasIndex(bp => bp.WalletId);
            entity.HasIndex(bp => bp.Status);
            entity.HasIndex(bp => bp.CreatedAt);
        });

        // BillProvider
        builder.Entity<BillProvider>(entity =>
        {
            entity.HasIndex(bp => bp.Code).IsUnique();
        });

        // CreditScore
        builder.Entity<CreditScore>(entity =>
        {
            entity.HasOne(cs => cs.User)
                .WithOne()
                .HasForeignKey<CreditScore>(cs => cs.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(cs => cs.UserId).IsUnique();
        });

        // CreditScoreFactor
        builder.Entity<CreditScoreFactor>(entity =>
        {
            entity.HasOne(csf => csf.CreditScore)
                .WithMany(cs => cs.Factors)
                .HasForeignKey(csf => csf.CreditScoreId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Remittance
        builder.Entity<Remittance>(entity =>
        {
            entity.HasOne(r => r.SenderUser)
                .WithMany()
                .HasForeignKey(r => r.SenderUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.SenderWallet)
                .WithMany()
                .HasForeignKey(r => r.SenderWalletId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(r => r.SenderUserId);
            entity.HasIndex(r => r.Status);
            entity.HasIndex(r => r.CreatedAt);
        });

        // RemittancePartner
        builder.Entity<RemittancePartner>(entity =>
        {
            entity.HasIndex(rp => rp.Code).IsUnique();
        });

        // LoyaltyPoint
        builder.Entity<LoyaltyPoint>(entity =>
        {
            entity.HasOne(lp => lp.User)
                .WithOne()
                .HasForeignKey<LoyaltyPoint>(lp => lp.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(lp => lp.UserId).IsUnique();
        });

        // LoyaltyTransaction
        builder.Entity<LoyaltyTransaction>(entity =>
        {
            entity.HasOne(lt => lt.User)
                .WithMany()
                .HasForeignKey(lt => lt.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(lt => lt.Transaction)
                .WithMany()
                .HasForeignKey(lt => lt.TransactionId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(lt => lt.Wallet)
                .WithMany()
                .HasForeignKey(lt => lt.WalletId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(lt => lt.UserId);
            entity.HasIndex(lt => lt.CreatedAt);
        });

        // Referral
        builder.Entity<Referral>(entity =>
        {
            entity.HasOne(r => r.ReferrerUser)
                .WithMany()
                .HasForeignKey(r => r.ReferrerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.ReferredUser)
                .WithMany()
                .HasForeignKey(r => r.ReferredUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(r => r.ReferrerUserId);
            entity.HasIndex(r => r.ReferredUserId);
        });

        // Cashback
        builder.Entity<Cashback>(entity =>
        {
            entity.HasOne(cb => cb.User)
                .WithMany()
                .HasForeignKey(cb => cb.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(cb => cb.Wallet)
                .WithMany()
                .HasForeignKey(cb => cb.WalletId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(cb => cb.Transaction)
                .WithMany()
                .HasForeignKey(cb => cb.TransactionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(cb => cb.UserId);
            entity.HasIndex(cb => cb.CreatedAt);
        });

        // BuyNowPayLater
        builder.Entity<BuyNowPayLater>(entity =>
        {
            entity.HasOne(b => b.User)
                .WithMany()
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(b => b.Wallet)
                .WithMany()
                .HasForeignKey(b => b.WalletId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.Merchant)
                .WithMany()
                .HasForeignKey(b => b.MerchantId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(b => b.MerchantPayment)
                .WithMany()
                .HasForeignKey(b => b.MerchantPaymentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(b => b.UserId);
            entity.HasIndex(b => b.Status);
        });

        // BnplInstallment
        builder.Entity<BnplInstallment>(entity =>
        {
            entity.HasOne(bi => bi.BuyNowPayLater)
                .WithMany(b => b.Installments)
                .HasForeignKey(bi => bi.BuyNowPayLaterId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(bi => bi.BuyNowPayLaterId);
        });

        // OpenBankingAccount
        builder.Entity<OpenBankingAccount>(entity =>
        {
            entity.HasOne(ob => ob.User)
                .WithMany()
                .HasForeignKey(ob => ob.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(ob => ob.UserId);
        });

        // ApiKey
        builder.Entity<ApiKey>(entity =>
        {
            entity.HasOne(ak => ak.User)
                .WithMany()
                .HasForeignKey(ak => ak.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(ak => ak.Key).IsUnique();
            entity.HasIndex(ak => ak.UserId);
        });

        // Webhook
        builder.Entity<Webhook>(entity =>
        {
            entity.HasOne(w => w.User)
                .WithMany()
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(w => w.UserId);
        });

        // WebhookDelivery
        builder.Entity<WebhookDelivery>(entity =>
        {
            entity.HasOne(wd => wd.Webhook)
                .WithMany(w => w.Deliveries)
                .HasForeignKey(wd => wd.WebhookId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(wd => wd.WebhookId);
            entity.HasIndex(wd => wd.CreatedAt);
        });

        // PosDevice
        builder.Entity<PosDevice>(entity =>
        {
            entity.HasOne(pd => pd.Merchant)
                .WithMany()
                .HasForeignKey(pd => pd.MerchantId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pd => pd.AssignedUser)
                .WithMany()
                .HasForeignKey(pd => pd.AssignedUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(pd => pd.DeviceSerial).IsUnique();
            entity.HasIndex(pd => pd.MerchantId);
        });

        // PosTransaction
        builder.Entity<PosTransaction>(entity =>
        {
            entity.HasOne(pt => pt.PosDevice)
                .WithMany(pd => pd.Transactions)
                .HasForeignKey(pt => pt.PosDeviceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pt => pt.Wallet)
                .WithMany()
                .HasForeignKey(pt => pt.WalletId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(pt => pt.PosDeviceId);
            entity.HasIndex(pt => pt.CreatedAt);
        });

        // WhiteLabelTenant
        builder.Entity<WhiteLabelTenant>(entity =>
        {
            entity.HasIndex(wt => wt.TenantCode).IsUnique();
        });

        // TenantUser
        builder.Entity<TenantUser>(entity =>
        {
            entity.HasOne(tu => tu.Tenant)
                .WithMany(wt => wt.Users)
                .HasForeignKey(tu => tu.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(tu => tu.User)
                .WithMany()
                .HasForeignKey(tu => tu.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(tu => tu.TenantId);
            entity.HasIndex(tu => tu.UserId);
        });

        // Microloan
        builder.Entity<Microloan>(entity =>
        {
            entity.HasOne(ml => ml.User)
                .WithMany()
                .HasForeignKey(ml => ml.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ml => ml.Wallet)
                .WithMany()
                .HasForeignKey(ml => ml.WalletId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(ml => ml.UserId);
            entity.HasIndex(ml => ml.Status);
        });

        // MicroloanRepayment
        builder.Entity<MicroloanRepayment>(entity =>
        {
            entity.HasOne(mlr => mlr.Microloan)
                .WithMany(ml => ml.Repayments)
                .HasForeignKey(mlr => mlr.MicroloanId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(mlr => mlr.MicroloanId);
        });

        // Insurance
        builder.Entity<Insurance>(entity =>
        {
            entity.HasOne(i => i.User)
                .WithMany()
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(i => i.Wallet)
                .WithMany()
                .HasForeignKey(i => i.WalletId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(i => i.UserId);
            entity.HasIndex(i => i.Status);
        });

        // InsuranceClaim
        builder.Entity<InsuranceClaim>(entity =>
        {
            entity.HasOne(ic => ic.Insurance)
                .WithMany(i => i.Claims)
                .HasForeignKey(ic => ic.InsuranceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(ic => ic.InsuranceId);
        });

        // Investment
        builder.Entity<Investment>(entity =>
        {
            entity.HasOne(i => i.User)
                .WithMany()
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(i => i.Wallet)
                .WithMany()
                .HasForeignKey(i => i.WalletId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(i => i.UserId);
            entity.HasIndex(i => i.Status);
        });

        // InvestmentTransaction
        builder.Entity<InvestmentTransaction>(entity =>
        {
            entity.HasOne(it => it.Investment)
                .WithMany(i => i.Transactions)
                .HasForeignKey(it => it.InvestmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(it => it.InvestmentId);
            entity.HasIndex(it => it.CreatedAt);
        });

        // SavingsGoal
        builder.Entity<SavingsGoal>(entity =>
        {
            entity.HasOne(sg => sg.User)
                .WithMany()
                .HasForeignKey(sg => sg.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(sg => sg.Wallet)
                .WithMany()
                .HasForeignKey(sg => sg.WalletId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(sg => sg.UserId);
            entity.HasIndex(sg => sg.Status);
        });

        // AiChat
        builder.Entity<AiChat>(entity =>
        {
            entity.HasOne(ac => ac.User)
                .WithMany()
                .HasForeignKey(ac => ac.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(ac => ac.UserId);
        });

        // AiMessage
        builder.Entity<AiMessage>(entity =>
        {
            entity.HasOne(am => am.AiChat)
                .WithMany(ac => ac.Messages)
                .HasForeignKey(am => am.AiChatId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(am => am.AiChatId);
            entity.HasIndex(am => am.CreatedAt);
        });

        // RefreshToken
        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasOne(rt => rt.User)
                .WithMany()
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(rt => rt.Token).IsUnique();
            entity.HasIndex(rt => rt.UserId);
            entity.HasIndex(rt => rt.ExpiresAt);
            entity.HasIndex(rt => rt.JwtId);
        });

        // WebAuthnCredential
        builder.Entity<WebAuthnCredential>(entity =>
        {
            entity.HasOne(wc => wc.User)
                .WithMany()
                .HasForeignKey(wc => wc.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(wc => wc.UserId);
            entity.HasIndex(wc => wc.CredentialId).IsUnique();
        });

        // WebAuthnChallenge
        builder.Entity<WebAuthnChallenge>(entity =>
        {
            entity.HasIndex(wc => wc.Challenge);
            entity.HasIndex(wc => wc.ExpiresAt);
        });
    }
}
