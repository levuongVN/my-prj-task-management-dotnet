using System.Text.RegularExpressions;
using TaskFlow.Application.DTOs.Labels;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Services;

public class LabelService
(
    ILabelRepository labelRepository
) : ILabelService
{
    private readonly ILabelRepository _labelRepository = labelRepository;

    private const string DefaultColor = "#2563eb";

    public async Task<List<LabelDto>> GetAllAsync(Guid userId)
    {
        var labels = await _labelRepository.GetByUserAsync(userId);

        return labels.Select(Map).ToList();
    }

    public async Task<LabelDto> CreateAsync(Guid userId, LabelRequest request)
    {
        var name = ValidateName(request.Name);
        var color = ValidateColor(request.Color);

        if (await _labelRepository.NameExistsAsync(userId, name))
        {
            throw new ArgumentException("Label name already exists");
        }

        var label = new Label
        {
            Id = Guid.NewGuid(),
            Name = name,
            Color = color,
            UserId = userId
        };

        await _labelRepository.AddAsync(label);
        await _labelRepository.SaveChangesAsync();

        return Map(label);
    }

    public async Task<LabelDto> UpdateAsync(Guid labelId, Guid userId, LabelRequest request)
    {
        var label = await _labelRepository.GetByIdAsync(labelId, userId);

        if (label == null)
        {
            throw new KeyNotFoundException("Label not found");
        }

        var name = ValidateName(request.Name);
        var color = ValidateColor(request.Color);

        if (await _labelRepository.NameExistsAsync(userId, name, excludeId: labelId))
        {
            throw new ArgumentException("Label name already exists");
        }

        label.Name = name;
        label.Color = color;

        _labelRepository.Update(label);
        await _labelRepository.SaveChangesAsync();

        return Map(label);
    }

    public async Task DeleteAsync(Guid labelId, Guid userId)
    {
        var label = await _labelRepository.GetByIdAsync(labelId, userId);

        if (label == null)
        {
            throw new KeyNotFoundException("Label not found");
        }

        // Hard delete - join rows cascade theo, task/project còn lại vô sự
        _labelRepository.Delete(label);

        await _labelRepository.SaveChangesAsync();
    }

    private static string ValidateName(string name)
    {
        var trimmed = name.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            throw new ArgumentException("Label name is required");
        }

        if (trimmed.Length > 50)
        {
            throw new ArgumentException("Label name must be 50 characters or fewer");
        }

        return trimmed;
    }

    private static string ValidateColor(string? color)
    {
        // Rỗng/không gửi -> dùng màu mặc định thay vì chặn
        if (string.IsNullOrWhiteSpace(color))
        {
            return DefaultColor;
        }

        var trimmed = color.Trim().ToUpper();

        if (!Regex.IsMatch(trimmed, "^#[0-9A-F]{6}$"))
        {
            throw new ArgumentException("Color must be a hex value like #RRGGBB");
        }

        return trimmed;
    }

    private static LabelDto Map(Label label)
    {
        return new LabelDto
        {
            Id = label.Id,
            Name = label.Name,
            Color = label.Color
        };
    }
}
