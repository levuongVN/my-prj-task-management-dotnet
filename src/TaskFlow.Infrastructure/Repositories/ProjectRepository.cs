using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Repositories;

public class ProjectRepository(ApplicationDbContext context) : IProjectRepository
{
    private readonly ApplicationDbContext _context = context;

    public async Task<Project?> GetByIdAsync(Guid projectId, Guid userId)
    {
        return await _context.Projects
            .Include(x => x.Tasks.Where(t => !t.IsDeleted))
            .Include(x => x.Labels)
            .FirstOrDefaultAsync(x =>
                x.Id == projectId &&
                x.UserId == userId &&
                !x.IsDeleted
            );
    }

    public async Task<List<Project>> GetAllByUserAsync(Guid userId)
    {
        return await _context.Projects
            .Where(x => x.UserId == userId && !x.IsDeleted)
            .Include(x => x.Tasks.Where(t => !t.IsDeleted))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<(List<Project> Items, int TotalCount)> GetPagedByUserAsync(
        Guid userId,
        int page,
        int pageSize,
        Guid? labelId = null
    )
    {
        var query = _context.Projects
            .Where(x => x.UserId == userId && !x.IsDeleted);

        if (labelId.HasValue)
        {
            query = query.Where(x => x.Labels.Any(l => l.Id == labelId.Value));
        }

        query = query.OrderByDescending(x => x.CreatedAt);

        var totalCount = await query.CountAsync();

        var items = await query
            .Include(x => x.Tasks.Where(t => !t.IsDeleted))
            .Include(x => x.Labels)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task AddAsync(Project project)
    {
        await _context.Projects.AddAsync(project);
    }

    public void Update(Project project)
    {
        _context.Projects.Update(project);
    }

    public void Delete(Project project)
    {
        project.IsDeleted = true;
        _context.Projects.Update(project);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}