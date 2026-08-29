using Microsoft.AspNetCore.SignalR;
using TaskFlow.API.Hubs;
using TaskFlow.Application.Features.Notifications.DTOs;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;

namespace TaskFlow.API.Services;

public class NotificationSender : INotificationSender
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationSender(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendNotificationAsync(Guid userId, Notification notification)
    {
        var dto = new NotificationDto
        {
            Id = notification.Id,
            Type = notification.Type,
            Title = notification.Title,
            Message = notification.Message,
            TaskId = notification.TaskId,
            ProjectId = notification.ProjectId,
            MeetingId = notification.MeetingId,
            IsRead = notification.ReadAt != null,
            CreatedAt = notification.CreatedAt
        };

        await _hubContext.Clients
            .User(userId.ToString())
            .SendAsync("NotificationReceived", dto);
    }
}
