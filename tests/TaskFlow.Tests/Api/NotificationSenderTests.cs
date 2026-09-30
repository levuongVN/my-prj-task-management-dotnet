using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using TaskFlow.API.Hubs;
using TaskFlow.API.Services;
using TaskFlow.Application.Features.Notifications.DTOs;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Tests.Api;

public class NotificationSenderTests
{
    private readonly IHubContext<NotificationHub> _hub = Substitute.For<IHubContext<NotificationHub>>();
    private readonly IHubClients _clients = Substitute.For<IHubClients>();
    private readonly IClientProxy _proxy = Substitute.For<IClientProxy>();

    private NotificationSender Sut()
    {
        _hub.Clients.Returns(_clients);
        _clients.User(Arg.Any<string>()).Returns(_proxy);
        return new NotificationSender(_hub);
    }

    private static Notification MakeNotification() => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        Type = NotificationType.TaskDeadlineApproaching,
        Title = "Deadline sắp tới",
        Message = "Task ABC hết hạn trong 24h",
        TaskId = Guid.NewGuid(),
    };

    [Fact]
    public async Task Send_GuiToiDungUserVoiTenEventDung()
    {
        var notification = MakeNotification();

        await Sut().SendNotificationAsync(notification.UserId, notification);

        _clients.Received(1).User(notification.UserId.ToString());
        await _proxy.Received(1).SendCoreAsync(
            "NotificationReceived",
            Arg.Any<object?[]>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_MapDungTruongDto()
    {
        var notification = MakeNotification();
        object?[]? sentArgs = null;
        _proxy
            .SendCoreAsync("NotificationReceived", Arg.Do<object?[]>(a => sentArgs = a), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await Sut().SendNotificationAsync(notification.UserId, notification);

        Assert.NotNull(sentArgs);
        var dto = Assert.IsType<NotificationDto>(sentArgs![0]);
        Assert.Equal(notification.Id, dto.Id);
        Assert.Equal(notification.Type, dto.Type);
        Assert.Equal(notification.Title, dto.Title);
        Assert.Equal(notification.Message, dto.Message);
        Assert.Equal(notification.TaskId, dto.TaskId);
        Assert.False(dto.IsRead);
    }

    [Fact]
    public async Task Send_DaRead_IsReadTrue()
    {
        var notification = MakeNotification();
        notification.ReadAt = DateTime.UtcNow;
        object?[]? sentArgs = null;
        _proxy
            .SendCoreAsync("NotificationReceived", Arg.Do<object?[]>(a => sentArgs = a), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await Sut().SendNotificationAsync(notification.UserId, notification);

        var dto = Assert.IsType<NotificationDto>(sentArgs![0]);
        Assert.True(dto.IsRead);
    }
}
