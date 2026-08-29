using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Interfaces;

public interface INotificationSender
{
    Task SendNotificationAsync(Guid userId, Notification notification);
}
