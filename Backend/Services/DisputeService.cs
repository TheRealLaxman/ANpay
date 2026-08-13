using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Models;
using ANpay.Api.Exceptions;

namespace ANpay.Api.Services;

public class DisputeService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DisputeService> _logger;

    public DisputeService(ApplicationDbContext context, ILogger<DisputeService> logger)
    {
        _context = context;
        _logger = logger;
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

    public async Task<Dispute?> GetDisputeByIdAsync(Guid id)
    {
        return await _context.Disputes
            .Include(d => d.User)
            .Include(d => d.AssignedTo)
            .Include(d => d.Messages)
                .ThenInclude(m => m.Sender)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<DisputeMessage> AddMessageAsync(Guid disputeId, string senderId, string content, bool isInternal = false)
    {
        var dispute = await _context.Disputes.FindAsync(disputeId)
            ?? throw new NotFoundException("Dispute not found");

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
            dispute.Status = DisputeStatus.Resolved;
            dispute.RefundAmount = refundAmount;
            dispute.Resolution = resolution;

            if (refundAmount > 0 && dispute.TransactionId.HasValue)
            {
                var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.Id == dispute.TransactionId);
                if (wallet != null)
                {
                    wallet.Balance += refundAmount.Value;
                    var refundTx = new Transaction
                    {
                        WalletId = wallet.Id,
                        Type = TransactionType.Refund,
                        Amount = refundAmount.Value,
                        BalanceBefore = wallet.Balance - refundAmount.Value,
                        BalanceAfter = wallet.Balance,
                        Description = $"Refund for dispute {disputeId}",
                        ReferenceNumber = $"RFD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                        Status = TransactionStatus.Completed
                    };
                    _context.Transactions.Add(refundTx);
                }
            }
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
