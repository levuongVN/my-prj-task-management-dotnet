using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Interfaces;

public interface ILabelRepository
{
    Task<List<Label>> GetByUserAsync(Guid userId);

    Task<Label?> GetByIdAsync(Guid id, Guid userId);

    // Verify + load labels khi gán vào task/project:
    // trả về số lượng ít hơn ids -> tồn tại id lạ/không thuộc user
    Task<List<Label>> GetByIdsAsync(Guid userId, List<Guid> ids);

    Task<bool> NameExistsAsync(Guid userId, string name, Guid? excludeId = null);

    Task AddAsync(Label label);

    void Update(Label label);

    void Delete(Label label);

    Task SaveChangesAsync();
}
