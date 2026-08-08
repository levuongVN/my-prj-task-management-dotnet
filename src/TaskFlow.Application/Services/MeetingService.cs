using TaskFlow.Application.Features.Meetings.DTOs;
using TaskFlow.Application.Features.Meetings.Interfaces;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Features.Meetings.Services;

public class MeetingService(
    IMeetingRepository meetingRepository,
    IProjectRepository projectRepository
) : IMeetingService
{
    private readonly IMeetingRepository _meetingRepository = meetingRepository;
    private readonly IProjectRepository _projectRepository = projectRepository;

    public async Task<List<MeetingResponse>> GetAllAsync(Guid userId)
    {
        var meetings = await _meetingRepository.GetAllByUserIdAsync(userId);
        return meetings.Select(Map).ToList();
    }

    public async Task<MeetingResponse> GetByIdAsync(Guid id, Guid userId)
    {
        var meeting = await _meetingRepository.GetByIdAsync(id, userId);

        if (meeting is null)
            throw new Exception("Meeting not found");

        return Map(meeting);
    }

    public async Task<MeetingResponse> CreateAsync(
        CreateOrUpdateMeetingRequest request,
        Guid userId
    )
    {
        if (request.ProjectId.HasValue)
        {
            var project = await _projectRepository.GetByIdAsync(
                request.ProjectId.Value,
                userId
            );

            if (project is null)
                throw new Exception("Project not found");
        }

        var meeting = new Meeting
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            StartAt = request.StartAt,
            UserId = userId,
            ProjectId = request.ProjectId,
        };

        await _meetingRepository.AddAsync(meeting);
        await _meetingRepository.SaveChangesAsync();

        // Reload để lấy Project navigation property
        var created = await _meetingRepository.GetByIdAsync(meeting.Id, userId);
        return Map(created!);
    }

    public async Task<MeetingResponse> UpdateAsync(
        Guid id,
        CreateOrUpdateMeetingRequest request,
        Guid userId
    )
    {
        var meeting = await _meetingRepository.GetByIdAsync(id, userId);

        if (meeting is null)
            throw new Exception("Meeting not found");

        if (request.ProjectId.HasValue)
        {
            var project = await _projectRepository.GetByIdAsync(
                request.ProjectId.Value,
                userId
            );

            if (project is null)
                throw new Exception("Project not found");
        }

        meeting.Title = request.Title;
        meeting.StartAt = request.StartAt;
        meeting.ProjectId = request.ProjectId;

        await _meetingRepository.SaveChangesAsync();

        return Map(meeting);
    }

    public async Task DeleteAsync(Guid id, Guid userId)
    {
        var meeting = await _meetingRepository.GetByIdAsync(id, userId);

        if (meeting is null)
            throw new Exception("Meeting not found");

        _meetingRepository.Delete(meeting);
        await _meetingRepository.SaveChangesAsync();
    }

    private static MeetingResponse Map(Meeting meeting) => new()
    {
        Id = meeting.Id,
        Title = meeting.Title,
        StartAt = meeting.StartAt,
        UserId = meeting.UserId,
        ProjectId = meeting.ProjectId,
        ProjectName = meeting.Project?.Name,
    };
}