using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SonOfAnton.Backend.Services;
using SonOfAnton.Shared.Core.Identity;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly NotificationStreamService _streamService;
    private readonly IUserContext _userContext;

    public NotificationsController(
        INotificationService notificationService, 
        NotificationStreamService streamService,
        IUserContext userContext)
    {
        _notificationService = notificationService;
        _streamService = streamService;
        _userContext = userContext;
    }

    private Guid GetUserId() => _userContext.CurrentUserId;

    [HttpGet]
    public async Task<ActionResult<List<NotificationDto>>> GetNotifications([FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var notifications = await _notificationService.GetUserNotificationsAsync(userId, skip, take);
        return Ok(notifications);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount()
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var count = await _notificationService.GetUnreadCountAsync(userId);
        return Ok(count);
    }

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        await _notificationService.MarkAsReadAsync(id, userId);
        return NoContent();
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        await _notificationService.MarkAllAsReadAsync(userId);
        return NoContent();
    }

    [HttpGet("stream")]
    public async Task Stream(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            Response.StatusCode = 401;
            return;
        }

        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        
        var connectionId = Guid.NewGuid().ToString();
        var channel = _streamService.CreateChannelForUser(userId, connectionId);

        try
        {
             await Response.WriteAsync($"event: connected\ndata: {connectionId}\n\n", cancellationToken);
             await Response.Body.FlushAsync(cancellationToken);

             await foreach (var notification in channel.Reader.ReadAllAsync(cancellationToken))
             {
                 var json = System.Text.Json.JsonSerializer.Serialize(notification, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
                 var message = $"data: {json}\n\n";
                 
                 await Response.WriteAsync(message, cancellationToken);
                 await Response.Body.FlushAsync(cancellationToken);
             }
        }
        catch (OperationCanceledException)
        {
            // İstemci ayrıldı
        }
        finally
        {
            _streamService.RemoveChannel(userId, connectionId);
        }
    }
}
