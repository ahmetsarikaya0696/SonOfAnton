using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Entities;
using SonOfAnton.Shared.Enums;

namespace SonOfAnton.Backend.Services;

public class NotificationService : INotificationService
{
    private readonly SonOfAntonDbContext _dbContext;
    private readonly NotificationStreamService _streamService;

    public NotificationService(SonOfAntonDbContext dbContext, NotificationStreamService streamService)
    {
        _dbContext = dbContext;
        _streamService = streamService;
    }

    public async Task<NotificationDto> CreateNotificationAsync(Guid userId, string title, string message, NotificationType type, string? link = null)
    {
        var entity = new NotificationEntity
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            Link = link
        };

        _dbContext.Notifications.Add(entity);
        await _dbContext.SaveChangesAsync();

        var dto = MapToDto(entity);

        // SSE ile anlık gönder
        await _streamService.PublishToUserAsync(userId, dto);

        return dto;
    }

    public async Task<List<NotificationDto>> GetUserNotificationsAsync(Guid userId, int skip = 0, int take = 50)
    {
        var entities = await _dbContext.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return entities.Select(MapToDto).ToList();
    }

    public async Task<int> GetUnreadCountAsync(Guid userId)
    {
        return await _dbContext.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task MarkAsReadAsync(Guid notificationId, Guid userId)
    {
        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification != null && !notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync(Guid userId)
    {
        await _dbContext.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, DateTime.UtcNow));
    }

    private static NotificationDto MapToDto(NotificationEntity entity)
    {
        return new NotificationDto
        {
            Id = entity.Id,
            Title = entity.Title,
            Message = entity.Message,
            Type = entity.Type,
            IsRead = entity.IsRead,
            CreatedAt = entity.CreatedAt,
            Link = entity.Link
        };
    }
}
