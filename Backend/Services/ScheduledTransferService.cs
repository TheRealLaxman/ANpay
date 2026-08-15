using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.DTOs;
using ANpay.Api.Exceptions;
using ANpay.Api.Models;

namespace ANpay.Api.Services;

public class ScheduledTransferService
{
    private readonly ApplicationDbContext _context;
    private readonly WalletService _walletService;
    private readonly ILogger<ScheduledTransferService> _logger;

    public ScheduledTransferService(
        ApplicationDbContext context,
        WalletService walletService,
        ILogger<ScheduledTransferService> logger)
    {
        _context = context;
        _walletService = walletService;
        _logger = logger;
    }

    public async Task<List<ScheduledTransferDto>> GetUserScheduledTransfersAsync(string userId)
    {
        return await _context.ScheduledTransfers
            .Include(st => st.SourceWallet)
            .Include(st => st.DestinationWallet)
            .Where(st => st.UserId == userId)
            .OrderBy(st => st.NextExecutionDate)
            .Select(st => new ScheduledTransferDto
            {
                Id = st.Id,
                SourceWalletName = st.SourceWallet!.WalletName,
                DestinationWalletName = st.DestinationWallet!.WalletName,
                Amount = st.Amount,
                Description = st.Description,
                RecurrenceType = st.RecurrenceType.ToString(),
                StartDate = st.StartDate,
                EndDate = st.EndDate,
                NextExecutionDate = st.NextExecutionDate,
                ExecutionCount = st.ExecutionCount,
                MaxExecutions = st.MaxExecutions,
                Status = st.Status.ToString(),
                CreatedAt = st.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<ScheduledTransferDto> CreateScheduledTransferAsync(string userId, CreateScheduledTransferDto dto)
    {
        var sourceWallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.Id == dto.SourceWalletId && w.UserId == userId)
            ?? throw new NotFoundException("Source wallet not found");

        var destWallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.Id == dto.DestinationWalletId)
            ?? throw new NotFoundException("Destination wallet not found");

        if (dto.SourceWalletId == dto.DestinationWalletId)
            throw new ValidationException("Source and destination wallets must be different");

        var nextDate = CalculateNextExecution(dto.RecurrenceType, dto.StartDate, dto.DayOfMonth);

        var scheduled = new ScheduledTransfer
        {
            UserId = userId,
            SourceWalletId = dto.SourceWalletId,
            DestinationWalletId = dto.DestinationWalletId,
            Amount = dto.Amount,
            Description = dto.Description,
            RecurrenceType = dto.RecurrenceType,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            DayOfMonth = dto.DayOfMonth,
            NextExecutionDate = nextDate,
            MaxExecutions = dto.MaxExecutions,
            Status = ScheduledTransferStatus.Active
        };

        _context.ScheduledTransfers.Add(scheduled);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Scheduled transfer created: {Id} for user {UserId}", scheduled.Id, userId);

        return new ScheduledTransferDto
        {
            Id = scheduled.Id,
            SourceWalletName = sourceWallet.WalletName,
            DestinationWalletName = destWallet.WalletName,
            Amount = scheduled.Amount,
            Description = scheduled.Description,
            RecurrenceType = scheduled.RecurrenceType.ToString(),
            StartDate = scheduled.StartDate,
            EndDate = scheduled.EndDate,
            NextExecutionDate = scheduled.NextExecutionDate,
            ExecutionCount = 0,
            MaxExecutions = scheduled.MaxExecutions,
            Status = scheduled.Status.ToString(),
            CreatedAt = scheduled.CreatedAt
        };
    }

    public async Task PauseScheduledTransferAsync(Guid id, string userId)
    {
        var transfer = await _context.ScheduledTransfers
            .FirstOrDefaultAsync(st => st.Id == id && st.UserId == userId)
            ?? throw new NotFoundException("Scheduled transfer not found");

        transfer.Status = ScheduledTransferStatus.Paused;
        await _context.SaveChangesAsync();
    }

    public async Task ResumeScheduledTransferAsync(Guid id, string userId)
    {
        var transfer = await _context.ScheduledTransfers
            .FirstOrDefaultAsync(st => st.Id == id && st.UserId == userId)
            ?? throw new NotFoundException("Scheduled transfer not found");

        transfer.Status = ScheduledTransferStatus.Active;
        transfer.NextExecutionDate = CalculateNextExecution(transfer.RecurrenceType, DateTime.UtcNow, transfer.DayOfMonth);
        await _context.SaveChangesAsync();
    }

    public async Task CancelScheduledTransferAsync(Guid id, string userId)
    {
        var transfer = await _context.ScheduledTransfers
            .FirstOrDefaultAsync(st => st.Id == id && st.UserId == userId)
            ?? throw new NotFoundException("Scheduled transfer not found");

        transfer.Status = ScheduledTransferStatus.Cancelled;
        await _context.SaveChangesAsync();
    }

    public async Task ExecuteDueTransfersAsync()
    {
        var now = DateTime.UtcNow;
        var dueTransfers = await _context.ScheduledTransfers
            .Where(st => st.Status == ScheduledTransferStatus.Active && st.NextExecutionDate <= now)
            .ToListAsync();

        foreach (var transfer in dueTransfers)
        {
            try
            {
                await _walletService.TransferAsync(transfer.UserId, new TransferDto
                {
                    SourceWalletId = transfer.SourceWalletId,
                    DestinationWalletId = transfer.DestinationWalletId,
                    Amount = transfer.Amount,
                    Description = $"{transfer.Description} (Scheduled #{transfer.ExecutionCount + 1})"
                });

                transfer.ExecutionCount++;
                transfer.LastError = null;

                if (transfer.MaxExecutions > 0 && transfer.ExecutionCount >= transfer.MaxExecutions)
                {
                    transfer.Status = ScheduledTransferStatus.Completed;
                }
                else if (transfer.EndDate.HasValue && now >= transfer.EndDate.Value)
                {
                    transfer.Status = ScheduledTransferStatus.Completed;
                }
                else
                {
                    transfer.NextExecutionDate = CalculateNextExecution(
                        transfer.RecurrenceType, transfer.NextExecutionDate, transfer.DayOfMonth);
                }

                _logger.LogInformation("Scheduled transfer {Id} executed successfully (#{Count})",
                    transfer.Id, transfer.ExecutionCount);
            }
            catch (Exception ex)
            {
                transfer.LastError = ex.Message;
                transfer.Status = ScheduledTransferStatus.Failed;
                _logger.LogError(ex, "Scheduled transfer {Id} failed", transfer.Id);
            }
        }

        await _context.SaveChangesAsync();
    }

    private static DateTime CalculateNextExecution(RecurrenceType type, DateTime from, int dayOfMonth)
    {
        return type switch
        {
            RecurrenceType.Weekly => from.AddDays(7),
            RecurrenceType.Biweekly => from.AddDays(14),
            RecurrenceType.Monthly => new DateTime(from.Year, from.Month, 1).AddMonths(1).AddDays(Math.Min(dayOfMonth, DateTime.DaysInMonth(from.Year, from.Month + 1)) - 1),
            RecurrenceType.Quarterly => new DateTime(from.Year, from.Month, 1).AddMonths(3).AddDays(Math.Min(dayOfMonth, DateTime.DaysInMonth(from.Year, from.Month + 3)) - 1),
            RecurrenceType.Yearly => new DateTime(from.Year + 1, 1, 1).AddDays(Math.Min(dayOfMonth - 1, 30)),
            _ => from.AddMonths(1)
        };
    }
}

public class ScheduledTransferDto
{
    public Guid Id { get; set; }
    public string SourceWalletName { get; set; } = string.Empty;
    public string DestinationWalletName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string RecurrenceType { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime NextExecutionDate { get; set; }
    public int ExecutionCount { get; set; }
    public int MaxExecutions { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateScheduledTransferDto
{
    public Guid SourceWalletId { get; set; }
    public Guid DestinationWalletId { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public RecurrenceType RecurrenceType { get; set; } = RecurrenceType.Monthly;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int DayOfMonth { get; set; } = 1;
    public int MaxExecutions { get; set; } = 0;
}
