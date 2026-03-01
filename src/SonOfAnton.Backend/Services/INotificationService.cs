using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Enums;

namespace SonOfAnton.Backend.Services;

public interface INotificationService
{
    Task<NotificationDto> CreateNotificationAsync(Guid userId, string title, string message, NotificationType type, string? link = null);
    Task<List<NotificationDto>> GetUserNotificationsAsync(Guid userId, int skip = 0, int take = 50);
    Task<int> GetUnreadCountAsync(Guid userId);
    Task MarkAsReadAsync(Guid notificationId, Guid userId);
    Task MarkAllAsReadAsync(Guid userId);
}
