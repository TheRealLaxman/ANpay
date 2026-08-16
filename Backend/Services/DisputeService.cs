using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Models;
using ANpay.Api.Exceptions;

namespace ANpay.Api.Services;

public class DisputeService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DisputeService> _logger;
    private readonly LedgerService _ledgerService;

    public DisputeService(ApplicationDbContext context, ILogger<DisputeService> logger, LedgerService ledgerService)
    {
        _context = context;
        _logger = logger;
        _ledgerService = ledgerService;
    }

    public async Task<Dispute> CreateDisputeAsync(string userId, CreateDisputeDto dto)
    {
        _logger.LogInformation("Creating dispute for user {UserId}: {Subject}", userId, dto.Subject);

        var dispute = new Dispute
        {
            UserId = userId,
            TransactionId = dto.TransactionId,
            Subject = dto.Subject,
            Description = dto.Description,
            Category = dto.Category,
            Priority = dto.Priority
        };

        _context.Disputes.Add(dispute);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Dispute {DisputeId} created", dispute.Id);
        return dispute;
    }

    public async Task<List<Dispute>> GetUserDisputesAsync(string userId)
    {
        return await _context.Disputes
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Dispute>> GetOpenDisputesAsync()
    {
        return await _context.Disputes
            .Where(d => d.Status == DisputeStatus.Open || d.Status == DisputeStatus.UnderReview)
            .Include(d => d.User)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task<Dispute?> GetDisputeByIdAsync(Guid id, string? userId = null)
    {
        var query = _context.Disputes
            .Include(d => d.User)
            .Include(d => d.AssignedTo)
            .Include(d => d.Messages)
                .ThenInclude(m => m.Sender)
            .Where(d => d.Id == id);

        // If userId provided, restrict to owner or admin roles
        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(d => d.UserId == userId);
        }

        return await query.FirstOrDefaultAsync();
    }

    public async Task<DisputeMessage> AddMessageAsync(Guid disputeId, string senderId, string content, bool isInternal = false)
    {
        var dispute = await _context.Disputes.FindAsync(disputeId)
            ?? throw new NotFoundException("Dispute not found");

        // Verify sender is dispute owner or has admin role
        if (dispute.UserId != senderId)
        {
            var user = await _context.Users.FindAsync(senderId);
            if (user == null || (user.Role != AppUserRole.SuperAdmin && user.Role != AppUserRole.MainBranchAdmin && user.Role != AppUserRole.BranchAdmin))
                throw new ValidationException("You can only add messages to your own disputes");
        }

        var message = new DisputeMessage
        {
            DisputeId = disputeId,
            SenderId = senderId,
            Content = content,
            IsInternal = isInternal
        };

        _context.DisputeMessages.Add(message);
        dispute.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return message;
    }

    public async Task UpdateStatusAsync(Guid disputeId, DisputeStatus status, string? resolution = null)
    {
        var dispute = await _context.Disputes.FindAsync(disputeId)
            ?? throw new NotFoundException("Dispute not found");

        dispute.Status = status;
        dispute.UpdatedAt = DateTime.UtcNow;

        if (status == DisputeStatus.Resolved || status == DisputeStatus.Rejected)
        {
            dispute.ResolvedAt = DateTime.UtcNow;
            dispute.Resolution = resolution;
        }

        await _context.SaveChangesAsync();
    }

    public async Task AssignDisputeAsync(Guid disputeId, string assignedToId)
    {
        var dispute = await _context.Disputes.FindAsync(disputeId)
            ?? throw new NotFoundException("Dispute not found");

        dispute.AssignedToId = assignedToId;
        dispute.Status = DisputeStatus.UnderReview;
        dispute.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task ResolveDisputeAsync(Guid disputeId, bool approve, decimal? refundAmount, string resolution)
    {
        var dispute = await _context.Disputes.FindAsync(disputeId)
            ?? throw new NotFoundException("Dispute not found");

        if (approve)
        {
            dispute.RefundAmount = refundAmount;
            dispute.Resolution = resolution;

            if (refundAmount.HasValue && refundAmount.Value > 0 && dispute.TransactionId.HasValue)
            {
                var transaction = await _context.Transactions.FirstOrDefaultAsync(t => t.Id == dispute.TransactionId.Value);
                if (transaction != null)
                {
                    // Validate refund amount does not exceed original transaction
                    if (refundAmount.Value > transaction.Amount)
                        throw new ValidationException($"Refund amount ({refundAmount.Value}) exceeds original transaction amount ({transaction.Amount})");

                    await using var dbTransaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.Id == transaction.WalletId);
                        if (wallet != null)
                        {
                            var balanceBefore = wallet.Balance;
                            wallet.Balance += refundAmount.Value;

                            var refundTx = new Transaction
                            {
                                WalletId = wallet.Id,
                                Type = TransactionType.Refund,
                                Amount = refundAmount.Value,
                                BalanceBefore = balanceBefore,
                                BalanceAfter = wallet.Balance,
                                Description = $"Refund for dispute {disputeId}",
                                ReferenceNumber = $"RFD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                                Status = TransactionStatus.Completed
                            };
                            _context.Transactions.Add(refundTx);

                            await _ledgerService.PostWalletDepositAsync(refundTx.Id, wallet.Id, refundAmount.Value, wallet.Currency);
                        }

                        await _context.SaveChangesAsync();
                        await dbTransaction.CommitAsync();
                    }
                    catch
                    {
                        await dbTransaction.RollbackAsync();
                        throw;
                    }
                }
            }

            dispute.Status = DisputeStatus.Resolved;
        }
        else
        {
            dispute.Status = DisputeStatus.Rejected;
            dispute.Resolution = resolution;
        }

        dispute.ResolvedAt = DateTime.UtcNow;
        dispute.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
}

public class CreateDisputeDto
{
    public Guid? TransactionId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DisputeCategory Category { get; set; }
    public DisputePriority Priority { get; set; } = DisputePriority.Medium;
}
