using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.DTOs.Projects;

public class CreateOrUpdateProjectRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime Due { get; set; }

    public ProjectStatus Status { get; set; }
}