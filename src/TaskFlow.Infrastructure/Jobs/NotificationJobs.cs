using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Jobs;

public class NotificationJobs
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notificationService;

    public NotificationJobs(
        ApplicationDbContext context,
        INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task SendDeadlineApproachingNotificationsAsync(int hoursAhead = 2)
    {
        var now = DateTime.UtcNow;
        var threshold = now.AddHours(hoursAhead);

        var tasks = await _context.Tasks
            .Where(t =>
                !t.IsDeleted &&
                t.Status != TaskFlow.Domain.Enums.TaskStatus.Done &&
                t.Deadline != null &&
                t.Deadline > now &&
                t.Deadline <= threshold)
            .ToListAsync();

        foreach (var task in tasks)
        {
            var remaining = task.Deadline!.Value - now;

            await _notificationService.CreateAndSendAsync(
                task.UserId,
                NotificationType.TaskDeadlineApproaching,
                "Task sắp đến hạn",
                $"Task \"{task.Title}\" sẽ đến hạn trong {FormatDuration(remaining)}.",
                taskId: task.Id,
                projectId: task.ProjectId,
                deduplicationKey: $"task-deadline-approaching-{task.Id}");
        }
    }

    public async Task SendOverdueNotificationsAsync()
    {
        var now = DateTime.UtcNow;

        var tasks = await _context.Tasks
            .Where(t =>
                !t.IsDeleted &&
                t.Status != TaskFlow.Domain.Enums.TaskStatus.Done &&
                t.Deadline != null &&
                t.Deadline < now)
            .ToListAsync();

        foreach (var task in tasks)
        {
            await _notificationService.CreateAndSendAsync(
                task.UserId,
                NotificationType.TaskOverdue,
                "Task quá hạn",
                $"Task \"{task.Title}\" đã quá hạn vào {task.Deadline!.Value:HH:mm dd/MM/yyyy}.",
                taskId: task.Id,
                projectId: task.ProjectId,
                deduplicationKey: $"task-overdue-{task.Id}");
        }
    }

    private static string FormatDuration(TimeSpan ts)
    {
        var sb = new System.Text.StringBuilder();

        if (ts.Days > 0)
        {
            sb.Append($"{ts.Days} ngày");
        }
        else if (ts.Hours > 0)
        {
            sb.Append($"{ts.Hours} giờ {(ts.Minutes > 0 ? $"{ts.Minutes} phút" : "")}".Trim());
        }
        else
        {
            sb.Append($"{Math.Max(ts.Minutes, 1)} phút");
        }

        return sb.ToString();
    }
}
