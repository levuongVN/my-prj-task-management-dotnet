using TaskFlow.Application.Features.Analytics.DTOs;

namespace TaskFlow.Application.Features.Analytics.Interfaces;

public interface IAnalyticsService
{
    Task<AnalyticsResponse> GetAsync(
        Guid userId,
        AnalyticsPeriod period,
        DateTime referenceDate
    );
}
