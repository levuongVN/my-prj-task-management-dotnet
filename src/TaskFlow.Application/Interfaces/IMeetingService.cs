using TaskFlow.Application.Common;
using TaskFlow.Application.Features.Meetings.DTOs;

namespace TaskFlow.Application.Features.Meetings.Interfaces;

public interface IMeetingService
{
    Task<List<MeetingResponse>> GetAllAsync(Guid userId);
    Task<PagedResult<MeetingResponse>> GetPagedAsync(Guid userId, int page, int pageSize);
    Task<MeetingResponse> GetByIdAsync(Guid id, Guid userId);
    Task<MeetingResponse> CreateAsync(CreateOrUpdateMeetingRequest request, Guid userId);
    Task<MeetingResponse> UpdateAsync(Guid id, CreateOrUpdateMeetingRequest request, Guid userId);
    Task DeleteAsync(Guid id, Guid userId);
}