namespace TaskFlow.Application.Features.Analytics.DTOs;

public class KpiResponse
{
    public int TotalTasks { get; set; }

    public int CompletedTasks { get; set; }

    public int InProgressTasks { get; set; }

    public int OverdueTasks { get; set; }

    public int CompletionRate { get; set; }

    public int CompletedTasksChange { get; set; }
    public int InProgressTasksChange { get; set; }
    public int OverdueTasksChange { get; set; }
    public int CompletionRateChange { get; set; }
}