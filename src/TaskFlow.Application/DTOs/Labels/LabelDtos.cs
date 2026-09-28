namespace TaskFlow.Application.DTOs.Labels;

public class LabelRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Color { get; set; }
}

public class LabelDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Color { get; set; } = string.Empty;
}
