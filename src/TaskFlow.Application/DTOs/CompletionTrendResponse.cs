namespace TaskFlow.Application.Features.Analytics.DTOs;

public class CompletionTrendResponse
{
    public string Label { get; set; } = string.Empty;
    public int Completed { get; set; }
    public int Overdue { get; set; }
}