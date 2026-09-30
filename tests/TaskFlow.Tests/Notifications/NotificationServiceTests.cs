using Microsoft.Extensions.Configuration;
using NSubstitute;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Interfaces;
using TaskFlow.Application.Services;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Tests.Notifications;

public class NotificationServiceTests
{
    private readonly INotificationRepository _notifications = Substitute.For<INotificationRepository>();
    private readonly INotificationSender _sender = Substitute.For<INotificationSender>();
    private readonly IEmailSender _email = Substitute.For<IEmailSender>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IConfiguration _config = Substitute.For<IConfiguration>();

    private NotificationService Sut() => new(_notifications, _sender, _email, _users, _config);

    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task GetByUserId_MapDungVaIsReadTuReadAt()
    {
        _notifications.GetByUserIdAsync(UserId, 50).Returns(
        [
            new Notification { UserId = UserId, Type = NotificationType.TaskOverdue, Title = "t1", Message = "m1" },
            new Notification { UserId = UserId, Title = "t2", Message = "m2", ReadAt = DateTime.UtcNow },
        ]);

        var res = await Sut().GetByUserIdAsync(UserId);

        Assert.False(res[0].IsRead);
        Assert.True(res[1].IsRead);
        Assert.Equal(NotificationType.TaskOverdue, res[0].Type);
    }

    [Fact]
    public async Task GetByUserId_TakeCustom_PassedXuongRepository()
    {
        _notifications.GetByUserIdAsync(UserId, 5).Returns([]);

        await Sut().GetByUserIdAsync(UserId, 5);

        await _notifications.Received(1).GetByUserIdAsync(UserId, 5);
    }

    [Fact]
    public async Task CountUnread_Passthrough()
    {
        _notifications.CountUnreadAsync(UserId).Returns(7);

        var res = await Sut().CountUnreadAsync(UserId);

        Assert.Equal(7, res);
    }

    [Fact]
    public async Task MarkAsRead_NotificationCuaUserKhac_TraFalse()
    {
        _notifications.GetByIdAsync(Arg.Any<Guid>()).Returns(new Notification { UserId = Guid.NewGuid() });

        var res = await Sut().MarkAsReadAsync(UserId, Guid.NewGuid());

        Assert.False(res);
        await _notifications.DidNotReceive().UpdateAsync(Arg.Any<Notification>());
    }

    [Fact]
    public async Task MarkAsRead_KhongTonTai_TraFalse()
    {
        _notifications.GetByIdAsync(Arg.Any<Guid>()).Returns((Notification?)null);

        Assert.False(await Sut().MarkAsReadAsync(UserId, Guid.NewGuid()));
    }

    [Fact]
    public async Task MarkAsRead_HopLe_SetReadAtVaUpdate()
    {
        var n = new Notification { UserId = UserId };
        _notifications.GetByIdAsync(n.Id).Returns(n);

        var res = await Sut().MarkAsReadAsync(UserId, n.Id);

        Assert.True(res);
        Assert.NotNull(n.ReadAt);
        await _notifications.Received(1).UpdateAsync(n);
    }

    [Fact]
    public async Task MarkAllAsRead_Passthrough()
    {
        _notifications.MarkAllAsReadAsync(UserId).Returns(3);

        Assert.Equal(3, await Sut().MarkAllAsReadAsync(UserId));
    }

    [Fact]
    public async Task CreateAndSend_DedupDaTonTai_TraLuonKhongSpam()
    {
        var existing = new Notification
        {
            UserId = UserId,
            Title = "old",
            Message = "m",
            DeduplicationKey = "deadline:task-1",
        };
        _notifications.GetByDeduplicationKeyAsync("deadline:task-1").Returns(existing);

        var res = await Sut().CreateAndSendAsync(
            UserId, NotificationType.TaskDeadlineApproaching, "t", "m", deduplicationKey: "deadline:task-1");

        Assert.Equal("old", res.Title);
        await _notifications.DidNotReceive().AddAsync(Arg.Any<Notification>());
        await _sender.DidNotReceive().SendNotificationAsync(Arg.Any<Guid>(), Arg.Any<Notification>());
        await _email.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task CreateAndSend_HopLe_PushRealtimeVaGuiEmail()
    {
        _users.GetByIdAsync(UserId).Returns(new User { Id = UserId, Email = "a@b.com", FullName = "A" });
        _notifications.AddAsync(Arg.Any<Notification>()).Returns(ci => ci.Arg<Notification>());

        var res = await Sut().CreateAndSendAsync(
            UserId, NotificationType.MeetingReminder, "Nhắc họp", "10h", meetingId: Guid.NewGuid());

        await _sender.Received(1).SendNotificationAsync(UserId, Arg.Any<Notification>());
        await _email.Received(1).SendAsync("a@b.com", Arg.Any<string>(), Arg.Any<string>());
        Assert.Equal("Nhắc họp", res.Title);
        Assert.False(res.IsRead);
    }

    [Fact]
    public async Task CreateAndSend_ThuaRace_KhongGuiTrung()
    {
        var record = new Notification { UserId = UserId, Title = "old-record" };
        _notifications.AddAsync(Arg.Any<Notification>()).Returns(record);

        var res = await Sut().CreateAndSendAsync(UserId, NotificationType.TaskOverdue, "t", "m", deduplicationKey: "k");

        await _sender.DidNotReceive().SendNotificationAsync(Arg.Any<Guid>(), Arg.Any<Notification>());
        await _email.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        Assert.Equal("old-record", res.Title);
    }

    [Fact]
    public async Task CreateAndSend_SmtpLoi_VanTraBinhThuong()
    {
        _users.GetByIdAsync(UserId).Returns(new User { Id = UserId, Email = "a@b.com", FullName = "A" });
        _email.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("smtp down")));
        _notifications.AddAsync(Arg.Any<Notification>()).Returns(ci => ci.Arg<Notification>());

        var res = await Sut().CreateAndSendAsync(UserId, NotificationType.TaskOverdue, "t", "m");

        Assert.Equal("t", res.Title);
        await _sender.Received(1).SendNotificationAsync(UserId, Arg.Any<Notification>());
    }

    [Fact]
    public async Task CreateAndSend_KhongDedup_DeduplicationKeyRong()
    {
        _notifications.AddAsync(Arg.Any<Notification>()).Returns(ci => ci.Arg<Notification>());

        await Sut().CreateAndSendAsync(UserId, NotificationType.TaskOverdue, "t", "m");

        await _notifications.Received(1).AddAsync(Arg.Is<Notification>(n => n.DeduplicationKey == string.Empty));
    }
}
