using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ANpay.Api.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId))
        {
            Context.Items["MissingUserIdentifier"] = true;
            await base.OnConnectedAsync();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
        }
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendNotificationToUser(string userId, string title, string message, string type)
    {
        var callerId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(callerId))
            return;

        var callerRole = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (callerId != userId && callerRole != "SuperAdmin" && callerRole != "MainBranchAdmin" && callerRole != "BranchAdmin")
            return;

        await Clients.Group($"user_{userId}").SendAsync("ReceiveNotification", new
        {
            title,
            message,
            type,
            timestamp = DateTime.UtcNow
        });
    }

    public async Task SendBalanceUpdate(string userId, decimal newBalance)
    {
        var callerId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(callerId))
            return;

        var callerRole = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (callerId != userId && callerRole != "SuperAdmin" && callerRole != "MainBranchAdmin")
            return;

        await Clients.Group($"user_{userId}").SendAsync("BalanceUpdated", new
        {
            balance = newBalance,
            timestamp = DateTime.UtcNow
        });
    }

    public async Task SendTransactionUpdate(string userId, string transactionId, string status)
    {
        var callerId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(callerId))
            return;

        var callerRole = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (callerId != userId && callerRole != "SuperAdmin" && callerRole != "MainBranchAdmin")
            return;

        await Clients.Group($"user_{userId}").SendAsync("TransactionUpdated", new
        {
            transactionId,
            status,
            timestamp = DateTime.UtcNow
        });
    }
}
