using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Models;
using ANpay.Api.Exceptions;

namespace ANpay.Api.Services;

public class ApprovalService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ApprovalService> _logger;

    public ApprovalService(ApplicationDbContext context, ILogger<ApprovalService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApprovalRequest> CreateAsync(string userId, ApprovalType type, string targetEntity, Guid? targetEntityId, decimal amount, string currency, string description)
    {
        var request = new ApprovalRequest
        {
            RequestedById = userId,
            Type = type,
            TargetEntity = targetEntity,
            TargetEntityId = targetEntityId,
            Amount = amount,
            Currency = currency,
            Description = description,
            Status = ApprovalStatus.Pending
        };

        _context.ApprovalRequests.Add(request);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Approval request created: {Type} by {UserId}", type, userId);
        return request;
    }

    public async Task<ApprovalRequest> ApproveAsync(Guid requestId, string approverId, string notes = "")
    {
        var request = await _context.ApprovalRequests.FindAsync(requestId)
            ?? throw new NotFoundException("Approval request not found");

        if (request.Status != ApprovalStatus.Pending)
            throw new ValidationException("Request is not pending");

        request.ApprovedById = approverId;
        request.Status = ApprovalStatus.Approved;
        request.Notes = notes;
        request.ReviewedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Approval request approved: {Id} by {ApproverId}", requestId, approverId);
        return request;
    }

    public async Task<ApprovalRequest> RejectAsync(Guid requestId, string approverId, string notes)
    {
        var request = await _context.ApprovalRequests.FindAsync(requestId)
            ?? throw new NotFoundException("Approval request not found");

        if (request.Status != ApprovalStatus.Pending)
            throw new ValidationException("Request is not pending");

        request.ApprovedById = approverId;
        request.Status = ApprovalStatus.Rejected;
        request.Notes = notes;
        request.ReviewedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Approval request rejected: {Id} by {ApproverId}", requestId, approverId);
        return request;
    }

    public async Task<List<ApprovalRequest>> GetPendingAsync(string? branchId = null)
    {
        var query = _context.ApprovalRequests
            .Include(ar => ar.RequestedBy)
            .Include(ar => ar.ApprovedBy)
            .Where(ar => ar.Status == ApprovalStatus.Pending);

        if (!string.IsNullOrEmpty(branchId) && Guid.TryParse(branchId, out var branchGuid))
            query = query.Where(ar => ar.RequestedBy.BranchId == branchGuid);

        return await query.OrderByDescending(ar => ar.CreatedAt).ToListAsync();
    }

    public async Task<List<ApprovalRequest>> GetUserRequestsAsync(string userId)
    {
        return await _context.ApprovalRequests
            .Include(ar => ar.ApprovedBy)
            .Where(ar => ar.RequestedById == userId)
            .OrderByDescending(ar => ar.CreatedAt)
            .ToListAsync();
    }
}
