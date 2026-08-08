namespace TaskFlow.Application.Features.Meetings.DTOs;

public class MeetingResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public Guid UserId { get; set; }
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
}

public class CreateOrUpdateMeetingRequest
{
    public string Title { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public Guid? ProjectId { get; set; }
}