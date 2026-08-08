namespace TaskFlow.Application.Features.Analytics.DTOs;

public class StatusDistributionResponse
{
    public int Todo { get; set; }

    public int InProgress { get; set; }

    public int InReview { get; set; }

    public int Done { get; set; }
}