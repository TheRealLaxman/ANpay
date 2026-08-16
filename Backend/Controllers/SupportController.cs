using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ANpay.Api.Services;
using ANpay.Api.Models;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SupportController : ControllerBase
{
    private readonly SupportService _supportService;
    private string UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";

    public SupportController(SupportService supportService)
    {
        _supportService = supportService;
    }

    [HttpPost("tickets")]
    public async Task<IActionResult> CreateTicket([FromBody] CreateTicketDto dto)
    {
        var ticket = await _supportService.CreateTicketAsync(UserId, dto.Subject, dto.Description, dto.Category, dto.Priority);
        return Ok(ticket);
    }

    [HttpGet("tickets/my")]
    public async Task<IActionResult> GetMyTickets()
    {
        var tickets = await _supportService.GetUserTicketsAsync(UserId);
        return Ok(tickets);
    }

    [HttpGet("tickets/open")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin,Official")]
    public async Task<IActionResult> GetOpenTickets()
    {
        var tickets = await _supportService.GetOpenTicketsAsync();
        return Ok(tickets);
    }

    [HttpGet("tickets/{id}")]
    public async Task<IActionResult> GetTicket(Guid id)
    {
        var ticket = await _supportService.GetTicketAsync(id);
        if (ticket == null) return NotFound();
        if (ticket.UserId != UserId && !User.IsInRole("SuperAdmin") && !User.IsInRole("BranchAdmin") && !User.IsInRole("MainBranchAdmin"))
            return Forbid();
        return Ok(ticket);
    }

    [HttpPost("tickets/{id}/messages")]
    public async Task<IActionResult> AddMessage(Guid id, [FromBody] AddMessageDto dto)
    {
        var ticket = await _supportService.GetTicketAsync(id);
        if (ticket == null) return NotFound();
        if (ticket.UserId != UserId && !User.IsInRole("SuperAdmin") && !User.IsInRole("BranchAdmin") && !User.IsInRole("MainBranchAdmin"))
            return Forbid();
        var message = await _supportService.AddMessageAsync(id, UserId, dto.Content, dto.IsInternal);
        return Ok(message);
    }

    [HttpPost("tickets/{id}/assign")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin")]
    public async Task<IActionResult> AssignTicket(Guid id, [FromBody] AssignTicketDto dto)
    {
        var ticket = await _supportService.AssignTicketAsync(id, dto.AssigneeId);
        return Ok(ticket);
    }

    [HttpPost("tickets/{id}/status")]
    [Authorize(Roles = "SuperAdmin,MainBranchAdmin,BranchAdmin,Official")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusDto dto)
    {
        var ticket = await _supportService.UpdateStatusAsync(id, dto.Status);
        return Ok(ticket);
    }
}

public class CreateTicketDto
{
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TicketCategory Category { get; set; }
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
}

public class AddMessageDto
{
    public string Content { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
}

public class AssignTicketDto
{
    public string AssigneeId { get; set; } = string.Empty;
}

public class UpdateStatusDto
{
    public TicketStatus Status { get; set; }
}
