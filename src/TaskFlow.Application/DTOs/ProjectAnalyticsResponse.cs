namespace TaskFlow.Application.Features.Analytics.DTOs;

public class ProjectAnalyticsResponse
{
    public Guid ProjectId { get; set; }

    public string ProjectName { get; set; } = "";

    public int TotalTasks { get; set; }

    public int CompletedTasks { get; set; }

    public int Progress { get; set; }
}