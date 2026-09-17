using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly ApplicationDbContext _context;

    public NotificationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Notification>> GetByUserIdAsync(Guid userId, int take = 50)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<int> CountUnreadAsync(Guid userId)
    {
        return await _context.Notifications
            .CountAsync(n => n.UserId == userId && n.ReadAt == null);
    }

    public async Task<Notification?> GetByIdAsync(Guid id)
    {
        return await _context.Notifications.FindAsync(id);
    }

    public async Task<Notification> AddAsync(Notification notification)
    {
        _context.Notifications.Add(notification);

        try
        {
            await _context.SaveChangesAsync();
            return notification;
        }
        catch (DbUpdateException)
        {
            // Unique index trên DeduplicationKey chặn insert thứ 2 khi 2 job
            // Hangfire chạy đồng thời cùng tạo 1 notification. Thay vì crash job,
            // ta trả về bản ghi đã tồn tại (service dùng ReferenceEquals để biết
            // "thua race" và bỏ qua bước gửi realtime trùng).
            _context.Entry(notification).State = EntityState.Detached;

            // Dedup key rỗng => không phải lỗi trùng dedup, ném tiếp đúng lỗi gốc
            if (string.IsNullOrEmpty(notification.DeduplicationKey))
            {
                throw;
            }

            var existing = await _context.Notifications
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.DeduplicationKey == notification.DeduplicationKey);

            if (existing == null)
            {
                throw;
            }

            return existing;
        }
    }

    public async Task<Notification?> GetByDeduplicationKeyAsync(string deduplicationKey)
    {
        return await _context.Notifications
            .FirstOrDefaultAsync(n => n.DeduplicationKey == deduplicationKey);
    }

    public async Task<Notification> UpdateAsync(Notification notification)
    {
        _context.Notifications.Update(notification);
        await _context.SaveChangesAsync();
        return notification;
    }

    public async Task<int> MarkAllAsReadAsync(Guid userId)
    {
        // ExecuteUpdateAsync sinh 1 câu UPDATE ... WHERE UserId=... AND ReadAt IS NULL
        // -> đánh dấu toàn bộ unread (không giới hạn 50), không cần nạp entity,
        // tránh N+1 (mỗi notification 1 SaveChanges như code cũ)
        return await _context.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s =>
                s.SetProperty(n => n.ReadAt, DateTime.UtcNow));
    }
}
