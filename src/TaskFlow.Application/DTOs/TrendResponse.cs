namespace TaskFlow.Application.Features.Analytics.DTOs;

public class TrendResponse
{
    public string Label { get; set; } = "";

    public int Completed { get; set; }

    public int Created { get; set; }
}