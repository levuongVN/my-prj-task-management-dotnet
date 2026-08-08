namespace TaskFlow.Application.Features.Analytics.DTOs;

public class AnalyticsResponse
{
    public KpiResponse Kpi { get; set; } = new();

    public StatusDistributionResponse Status { get; set; } = new();

    public PriorityDistributionResponse Priority { get; set; } = new();

    public List<ProjectAnalyticsResponse> TopProjects { get; set; } = [];

    public List<TrendResponse> ActivityTrend { get; set; } = [];

    public List<CompletionTrendResponse> CompletionTrend { get; set; } = [];
    public List<RecentActivityResponse> RecentActivity { get; set; } = [];
    public InsightResponse Insight { get; set; } = new();

}