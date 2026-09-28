using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Repositories;

public class LabelRepository(
    ApplicationDbContext context
) : ILabelRepository
{
    private readonly ApplicationDbContext _context = context;

    public async Task<List<Label>> GetByUserAsync(Guid userId)
    {
        return await _context.Labels
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<Label?> GetByIdAsync(Guid id, Guid userId)
    {
        return await _context.Labels
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.UserId == userId
            );
    }

    public async Task<List<Label>> GetByIdsAsync(Guid userId, List<Guid> ids)
    {
        return await _context.Labels
            .Where(x => x.UserId == userId && ids.Contains(x.Id))
            .ToListAsync();
    }

    public async Task<bool> NameExistsAsync(Guid userId, string name, Guid? excludeId = null)
    {
        var normalized = name.Trim().ToLower();

        return await _context.Labels
            .AnyAsync(x =>
                x.UserId == userId &&
                x.Name.Trim().ToLower() == normalized &&
                (excludeId == null || x.Id != excludeId)
            );
    }

    public async Task AddAsync(Label label)
    {
        await _context.Labels.AddAsync(label);
    }

    public void Update(Label label)
    {
        _context.Labels.Update(label);
    }

    public void Delete(Label label)
    {
        _context.Labels.Remove(label);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
