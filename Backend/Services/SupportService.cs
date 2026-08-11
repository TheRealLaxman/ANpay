using Microsoft.EntityFrameworkCore;
using ANpay.Api.Data;
using ANpay.Api.Models;
using ANpay.Api.Exceptions;

namespace ANpay.Api.Services;

public class SupportService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SupportService> _logger;

    public SupportService(ApplicationDbContext context, ILogger<SupportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SupportTicket> CreateTicketAsync(string userId, string subject, string description, TicketCategory category, TicketPriority priority = TicketPriority.Medium)
    {
        var ticket = new SupportTicket
        {
            UserId = userId,
            Subject = subject,
            Description = description,
            Category = category,
            Priority = priority,
            Status = TicketStatus.Open
        };

        _context.SupportTickets.Add(ticket);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Support ticket created: {Id} by {UserId}", ticket.Id, userId);
        return ticket;
    }

    public async Task<TicketMessage> AddMessageAsync(Guid ticketId, string senderId, string content, bool isInternal = false)
    {
        var ticket = await _context.SupportTickets.FindAsync(ticketId)
            ?? throw new NotFoundException("Ticket not found");

        var message = new TicketMessage
        {
            TicketId = ticketId,
            SenderId = senderId,
            Content = content,
            IsInternal = isInternal
        };

        _context.TicketMessages.Add(message);

        ticket.UpdatedAt = DateTime.UtcNow;
        if (ticket.Status == TicketStatus.Open)
            ticket.Status = TicketStatus.InProgress;

        await _context.SaveChangesAsync();
        return message;
    }

    public async Task<SupportTicket> AssignTicketAsync(Guid ticketId, string assigneeId)
    {
        var ticket = await _context.SupportTickets.FindAsync(ticketId)
            ?? throw new NotFoundException("Ticket not found");

        ticket.AssignedToId = assigneeId;
        ticket.Status = TicketStatus.InProgress;
        ticket.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return ticket;
    }

    public async Task<SupportTicket> UpdateStatusAsync(Guid ticketId, TicketStatus status)
    {
        var ticket = await _context.SupportTickets.FindAsync(ticketId)
            ?? throw new NotFoundException("Ticket not found");

        ticket.Status = status;
        ticket.UpdatedAt = DateTime.UtcNow;
        if (status == TicketStatus.Resolved)
            ticket.ResolvedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return ticket;
    }

    public async Task<List<SupportTicket>> GetUserTicketsAsync(string userId)
    {
        return await _context.SupportTickets
            .Include(st => st.AssignedTo)
            .Where(st => st.UserId == userId)
            .OrderByDescending(st => st.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<SupportTicket>> GetOpenTicketsAsync()
    {
        return await _context.SupportTickets
            .Include(st => st.User)
            .Include(st => st.AssignedTo)
            .Where(st => st.Status != TicketStatus.Closed && st.Status != TicketStatus.Resolved)
            .OrderByDescending(st => st.Priority)
            .ThenByDescending(st => st.CreatedAt)
            .ToListAsync();
    }

    public async Task<SupportTicket> GetTicketAsync(Guid ticketId)
    {
        return await _context.SupportTickets
            .Include(st => st.User)
            .Include(st => st.AssignedTo)
            .Include(st => st.Messages)
                .ThenInclude(m => m.Sender)
            .FirstOrDefaultAsync(st => st.Id == ticketId)
            ?? throw new NotFoundException("Ticket not found");
    }
}
