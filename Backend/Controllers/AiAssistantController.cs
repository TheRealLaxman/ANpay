using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ANpay.Api.Models;
using ANpay.Api.Services;

namespace ANpay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AiAssistantController : ControllerBase
{
    private readonly AiAssistantService _aiService;
    private readonly ILogger<AiAssistantController> _logger;

    public AiAssistantController(AiAssistantService aiService, ILogger<AiAssistantController> logger)
    {
        _aiService = aiService;
        _logger = logger;
    }

    [HttpPost("chat")]
    public async Task<ActionResult<AiChat>> CreateChat([FromBody] CreateChatRequest? request = null)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var chat = await _aiService.CreateChatAsync(userId, request?.Title);
        return Ok(chat);
    }

    [HttpGet("chats")]
    public async Task<ActionResult<List<AiChat>>> GetMyChats()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var chats = await _aiService.GetUserChatsAsync(userId);
        return Ok(chats);
    }

    [HttpGet("chat/{id}")]
    public async Task<ActionResult<AiChat>> GetChat(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var chat = await _aiService.GetChatByIdAsync(id, userId);
        if (chat == null) return NotFound();
        return Ok(chat);
    }

    [HttpPost("chat/{id}/message")]
    public async Task<ActionResult<AiMessage>> SendMessage(Guid id, [FromBody] SendMessageRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var message = await _aiService.SendMessageAsync(id, userId, request.Content);
        return Ok(message);
    }

    [HttpGet("chat/{id}/messages")]
    public async Task<ActionResult<List<AiMessage>>> GetChatMessages(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var messages = await _aiService.GetChatMessagesAsync(id, userId);
        return Ok(messages);
    }

    [HttpPost("chat/{id}/archive")]
    public async Task<ActionResult<AiChat>> ArchiveChat(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var chat = await _aiService.ArchiveChatAsync(id, userId);
        return Ok(chat);
    }

    private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
}

public class CreateChatRequest
{
    public string? Title { get; set; }
}

public class SendMessageRequest
{
    public string Content { get; set; } = string.Empty;
}
