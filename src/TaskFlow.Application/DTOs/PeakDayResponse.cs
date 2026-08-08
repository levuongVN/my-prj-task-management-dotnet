// InsightResponse.cs
namespace TaskFlow.Application.Features.Analytics.DTOs;

public class InsightResponse
{
    public string PeakDay { get; set; } = string.Empty;
    public int PeakDayCount { get; set; }
}