using TaskFlow.Application.Features.Analytics.DTOs;
using TaskFlow.Application.Features.Analytics.Interfaces;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.Analytics.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly ITaskRepository _taskRepository;

    private readonly IProjectRepository _projectRepository;

    public AnalyticsService(
        ITaskRepository taskRepository,
        IProjectRepository projectRepository)
    {
        _taskRepository = taskRepository;
        _projectRepository = projectRepository;
    }

    public async Task<AnalyticsResponse> GetAsync(
    Guid userId,
    AnalyticsPeriod period,
    DateTime referenceDate)
    {
        var projects =
            await _projectRepository.GetAllByUserAsync(userId);

        var allTasks = await _taskRepository.GetAllByUserIdAsync(userId);

        var periodTasks = FilterByPeriod(allTasks, period, referenceDate);
        var previousPeriodTasks = FilterByPreviousPeriod(allTasks, period, referenceDate);

        return new AnalyticsResponse
        {
            Kpi = BuildKpi(periodTasks, previousPeriodTasks),

            Status = BuildStatus(periodTasks),

            Priority = BuildPriority(periodTasks),

            TopProjects = BuildTopProjects(projects),

            ActivityTrend = BuildActivityTrend(periodTasks, period, referenceDate),

            CompletionTrend = BuildCompletionTrend(allTasks, period, referenceDate),

            RecentActivity = BuildRecentActivity(periodTasks),

            Insight = BuildInsight(periodTasks),

        };
    }

    private static List<TaskItem> FilterByPeriod(
    List<TaskItem> tasks,
    AnalyticsPeriod period,
    DateTime referenceDate)
    {

        var now = referenceDate;

        DateTime start;
        DateTime end;

        switch (period)
        {
            case AnalyticsPeriod.Week:

                var diff =
                    (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;

                start = now.Date.AddDays(-diff);

                end = start.AddDays(7);

                break;

            case AnalyticsPeriod.Month:

                start = new DateTime(
                    now.Year,
                    now.Month,
                    1);

                end = start.AddMonths(1);

                break;

            case AnalyticsPeriod.Quarter:

                var quarter = (now.Month - 1) / 3;

                start = new DateTime(
                    now.Year,
                    quarter * 3 + 1,
                    1);

                end = start.AddMonths(3);

                break;

            default:

                start = DateTime.MinValue;
                end = DateTime.MaxValue;

                break;
        }

        return tasks
            .Where(x =>
                x.CreatedAt >= start &&
                x.CreatedAt < end)
            .ToList();
    }
    // Sửa BuildKpi — nhận thêm previousTasks
    private static KpiResponse BuildKpi(
        List<TaskItem> tasks,
        List<TaskItem> previousTasks)
    {
        var total = tasks.Count;
        var completed = tasks.Count(x => x.Status == TaskFlow.Domain.Enums.TaskStatus.Done);
        var inProgress = tasks.Count(x => x.Status == TaskFlow.Domain.Enums.TaskStatus.InProgress);
        var overdue = tasks.Count(x =>
            x.Status != TaskFlow.Domain.Enums.TaskStatus.Done &&
            x.Deadline < DateTime.UtcNow);
        var rate = total == 0 ? 0 : completed * 100 / total;

        var prevTotal = previousTasks.Count;
        var prevCompleted = previousTasks.Count(x => x.Status == TaskFlow.Domain.Enums.TaskStatus.Done);
        var prevInProgress = previousTasks.Count(x => x.Status == TaskFlow.Domain.Enums.TaskStatus.InProgress);
        var prevOverdue = previousTasks.Count(x =>
            x.Status != TaskFlow.Domain.Enums.TaskStatus.Done &&
            x.Deadline < DateTime.UtcNow);
        var prevRate = prevTotal == 0 ? 0 : prevCompleted * 100 / prevTotal;

        return new KpiResponse
        {
            TotalTasks = total,
            CompletedTasks = completed,
            InProgressTasks = inProgress,
            OverdueTasks = overdue,
            CompletionRate = rate,

            CompletedTasksChange = completed - prevCompleted,
            InProgressTasksChange = inProgress - prevInProgress,
            OverdueTasksChange = overdue - prevOverdue,
            CompletionRateChange = rate - prevRate,
        };
    }
    private static StatusDistributionResponse BuildStatus(
    List<TaskItem> tasks)
    {
        return new StatusDistributionResponse
        {
            Todo =
                tasks.Count(x =>
                    x.Status == TaskFlow.Domain.Enums.TaskStatus.Todo),

            InProgress =
                tasks.Count(x => x.Status == TaskFlow.Domain.Enums.TaskStatus.InProgress),

            InReview = tasks.Count(x => x.Status == TaskFlow.Domain.Enums.TaskStatus.InPreview),

            Done = tasks.Count(x => x.Status == TaskFlow.Domain.Enums.TaskStatus.Done)
        };
    }
    private static PriorityDistributionResponse BuildPriority(
    List<TaskItem> tasks)
    {
        return new PriorityDistributionResponse
        {
            High =
                tasks.Count(x =>
                    x.Priority == TaskPriority.High),

            Medium =
                tasks.Count(x =>
                    x.Priority == TaskPriority.Medium),

            Low =
                tasks.Count(x =>
                    x.Priority == TaskPriority.Low)
        };
    }

    private static List<ProjectAnalyticsResponse> BuildTopProjects(List<Project> projects)
    {
        return projects.Select(p =>
            {
                var total =
                    p.Tasks.Count;

                var completed = p.Tasks.Count(x => x.Status == TaskFlow.Domain.Enums.TaskStatus.Done);

                return new ProjectAnalyticsResponse
                {
                    ProjectId = p.Id,
                    ProjectName = p.Name,

                    TotalTasks = total,

                    CompletedTasks = completed,

                    Progress =
                        total == 0
                            ? 0
                            : completed * 100 / total
                };
            }).OrderByDescending(x => x.Progress).ThenByDescending(x => x.CompletedTasks).Take(5).ToList();
    }
    private static List<TrendResponse> BuildActivityTrend(
    List<TaskItem> tasks,
    AnalyticsPeriod period,
    DateTime referenceDate)
    {
        var result = new List<TrendResponse>();
        var now = referenceDate;

        switch (period)
        {
            case AnalyticsPeriod.Week:

                var monday =
                    now.Date.AddDays(
                        -((7 + (int)now.DayOfWeek - (int)DayOfWeek.Monday) % 7));

                for (int i = 0; i < 7; i++)
                {
                    var day = monday.AddDays(i);

                    result.Add(new TrendResponse
                    {
                        Label = day.ToString("ddd"),

                        Created = tasks.Count(x =>
                            x.CreatedAt.Date == day.Date),

                        Completed = tasks.Count(x =>
                            x.Status == TaskFlow.Domain.Enums.TaskStatus.Done &&
                            x.UpdatedAt.Date == day.Date)
                    });
                }

                break;

            case AnalyticsPeriod.Month:

                var firstDay = new DateTime(now.Year, now.Month, 1);

                var ranges = new[]
                {
        (Start: firstDay, End: firstDay.AddDays(7)),
        (Start: firstDay.AddDays(7), End: firstDay.AddDays(14)),
        (Start: firstDay.AddDays(14), End: firstDay.AddDays(21)),
        (Start: firstDay.AddDays(21), End: firstDay.AddMonths(1))
    };

                for (int i = 0; i < ranges.Length; i++)
                {
                    result.Add(new TrendResponse
                    {
                        Label = $"Week {i + 1}",

                        Created = tasks.Count(x =>
                            x.CreatedAt >= ranges[i].Start &&
                            x.CreatedAt < ranges[i].End),

                        Completed = tasks.Count(x =>
                            x.Status == Domain.Enums.TaskStatus.Done &&
                            x.UpdatedAt >= ranges[i].Start &&
                            x.UpdatedAt < ranges[i].End)
                    });
                }

                break;

            case AnalyticsPeriod.Quarter:

                var quarter =
                    (now.Month - 1) / 3;

                for (int i = 0; i < 3; i++)
                {
                    var month =
                        quarter * 3 + i + 1;

                    result.Add(new TrendResponse
                    {
                        Label = new DateTime(
                            now.Year,
                            month,
                            1).ToString("MMM"),

                        Created = tasks.Count(x =>
                            x.CreatedAt.Month == month &&
                            x.CreatedAt.Year == now.Year),

                        Completed = tasks.Count(x =>
                            x.Status == TaskFlow.Domain.Enums.TaskStatus.Done &&
                            x.UpdatedAt.Month == month &&
                            x.UpdatedAt.Year == now.Year)
                    });
                }

                break;
        }

        return result;
    }
    private static List<CompletionTrendResponse> BuildCompletionTrend(
    List<TaskItem> allTasks,
    AnalyticsPeriod period,
    DateTime referenceDate)
    {
        var result = new List<CompletionTrendResponse>();
        var now = referenceDate;

        for (int i = 5; i >= 0; i--)
        {
            DateTime start, end;
            string label;

            switch (period)
            {
                case AnalyticsPeriod.Week:
                    // Thứ 2 của tuần hiện tại - i*7 ngày
                    var monday = now.Date.AddDays(
                        -((7 + (int)now.DayOfWeek - (int)DayOfWeek.Monday) % 7)
                    ).AddDays(-i * 7);
                    start = monday;
                    end = monday.AddDays(7);
                    var weekNo = System.Globalization.ISOWeek.GetWeekOfYear(monday);
                    label = $"W{weekNo}";
                    break;

                case AnalyticsPeriod.Month:
                    var refMonth = now.AddMonths(-i);
                    start = new DateTime(refMonth.Year, refMonth.Month, 1);
                    end = start.AddMonths(1);
                    label = start.ToString("MMM");
                    break;

                default: // Quarter
                    var refQuarter = now.AddMonths(-i * 3);
                    var q = (refQuarter.Month - 1) / 3;
                    start = new DateTime(refQuarter.Year, q * 3 + 1, 1);
                    end = start.AddMonths(3);
                    label = $"Q{q + 1}'{refQuarter.Year % 100:D2}";
                    break;
            }

            result.Add(new CompletionTrendResponse
            {
                Label = label,
                Completed = allTasks.Count(x =>
                    x.Status == TaskFlow.Domain.Enums.TaskStatus.Done &&
                    x.UpdatedAt >= start &&
                    x.UpdatedAt < end),
                Overdue = allTasks.Count(x =>
                    x.Status != TaskFlow.Domain.Enums.TaskStatus.Done &&
                    x.Deadline.HasValue &&
                    x.Deadline < DateTime.UtcNow &&
                    x.CreatedAt >= start &&
                    x.CreatedAt < end),
            });
        }

        return result;
    }

    // Thêm FilterByPreviousPeriod
    private static List<TaskItem> FilterByPreviousPeriod(
        List<TaskItem> tasks,
        AnalyticsPeriod period,
        DateTime referenceDate)
    {
        var now = referenceDate;
        DateTime start, end;

        switch (period)
        {
            case AnalyticsPeriod.Week:
                var diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
                end = now.Date.AddDays(-diff);       // Thứ 2 tuần này
                start = end.AddDays(-7);             // Thứ 2 tuần trước
                break;

            case AnalyticsPeriod.Month:
                end = new DateTime(now.Year, now.Month, 1);
                start = end.AddMonths(-1);
                break;

            case AnalyticsPeriod.Quarter:
                var quarter = (now.Month - 1) / 3;
                end = new DateTime(now.Year, quarter * 3 + 1, 1);
                start = end.AddMonths(-3);
                break;

            default:
                start = DateTime.MinValue;
                end = DateTime.MaxValue;
                break;
        }

        return tasks.Where(x => x.CreatedAt >= start && x.CreatedAt < end).ToList();
    }
    private static List<RecentActivityResponse> BuildRecentActivity(List<TaskItem> allTasks)
    {
        return allTasks
            .OrderByDescending(x => x.UpdatedAt)
            .Take(10)
            .Select(x =>
            {
                var (action, type) = x.Status switch
                {
                    TaskFlow.Domain.Enums.TaskStatus.Done => ("Completed", "completed"),
                    TaskFlow.Domain.Enums.TaskStatus.InProgress => ("In Progress", "created"),
                    TaskFlow.Domain.Enums.TaskStatus.InPreview => ("In Review", "review"),
                    _ when x.Deadline < DateTime.UtcNow => ("Overdue", "overdue"),
                    _ => ("Created", "created"),
                };

                return new RecentActivityResponse
                {
                    TaskTitle = x.Title,
                    Action = action,
                    Type = type,
                    OccurredAt = x.UpdatedAt,
                };
            })
            .ToList();
    }
    // BuildInsight trong AnalyticsService
    private static InsightResponse BuildInsight(List<TaskItem> allTasks)
    {
        var completedTasks = allTasks
            .Where(x => x.Status == TaskFlow.Domain.Enums.TaskStatus.Done)
            .ToList();

        if (!completedTasks.Any())
            return new InsightResponse { PeakDay = "—", PeakDayCount = 0 };

        var peakDay = completedTasks
            .GroupBy(x => x.UpdatedAt.DayOfWeek)
            .OrderByDescending(g => g.Count())
            .First();

        return new InsightResponse
        {
            PeakDay = peakDay.Key.ToString(),
            PeakDayCount = peakDay.Count(),
        };
    }
}
