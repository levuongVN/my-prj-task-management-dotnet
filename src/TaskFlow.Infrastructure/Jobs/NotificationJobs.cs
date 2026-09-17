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
                "Task deadline approaching",
                $"Task \"{task.Title}\" is due in {FormatDuration(remaining)}.",
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
                "Task overdue",
                $"Task \"{task.Title}\" was due on {task.Deadline!.Value:HH:mm dd/MM/yyyy}.",
                taskId: task.Id,
                projectId: task.ProjectId,
                deduplicationKey: $"task-overdue-{task.Id}");
        }
    }

    public async Task SendMeetingReminderNotificationsAsync(int hoursAhead = 2)
    {
        var now = DateTime.UtcNow;
        var threshold = now.AddHours(hoursAhead);

        // Meeting không có soft delete (delete là hard remove) nên không cần filter IsDeleted
        var meetings = await _context.Meetings
            .Where(m =>
                m.StartAt > now &&
                m.StartAt <= threshold)
            .ToListAsync();

        foreach (var meeting in meetings)
        {
            var remaining = meeting.StartAt - now;

            await _notificationService.CreateAndSendAsync(
                meeting.UserId,
                NotificationType.MeetingReminder,
                "Meeting reminder",
                $"Meeting \"{meeting.Title}\" starts in {FormatDuration(remaining)}.",
                projectId: meeting.ProjectId,
                meetingId: meeting.Id,
                deduplicationKey: $"meeting-reminder-{meeting.Id}");
        }
    }

    private static string FormatDuration(TimeSpan ts)
    {
        var sb = new System.Text.StringBuilder();

        if (ts.Days > 0)
        {
            sb.Append($"{ts.Days} day{(ts.Days > 1 ? "s" : "")}");
        }
        else if (ts.Hours > 0)
        {
            sb.Append($"{ts.Hours} hour{(ts.Hours > 1 ? "s" : "")}{(ts.Minutes > 0 ? $" {ts.Minutes} min" : "")}");
        }
        else
        {
            sb.Append($"{Math.Max(ts.Minutes, 1)} min");
        }

        return sb.ToString();
    }
}
