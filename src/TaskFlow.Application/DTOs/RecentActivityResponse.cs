namespace TaskFlow.Application.Features.Analytics.DTOs;

public class RecentActivityResponse
{
    public string TaskTitle { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}