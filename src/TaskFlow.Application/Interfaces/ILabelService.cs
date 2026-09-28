using TaskFlow.Application.DTOs.Labels;

namespace TaskFlow.Application.Interfaces;

public interface ILabelService
{
    Task<List<LabelDto>> GetAllAsync(Guid userId);

    Task<LabelDto> CreateAsync(Guid userId, LabelRequest request);

    Task<LabelDto> UpdateAsync(Guid labelId, Guid userId, LabelRequest request);

    Task DeleteAsync(Guid labelId, Guid userId);
}
