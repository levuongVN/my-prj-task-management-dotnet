using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Common;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Jobs;

// =============================================================================
// Job dọn session chat cũ - chống phình DB trên Supabase free tier (500MB).
//
// Cơ chế chạy (giống NotificationJobs): Hangfire scheduler đăng ký job với cron
// "0 3 * * *" (03:00 mỗi ngày). Khi tới giờ, Hangfire sinh 1 job record trong DB
// rồi worker (AddHangfireServer) gọi method CleanupOldSessionsAsync này.
//
// Vì sao xoá được message mà không cần code xoá message?
//   FK AiChatMessage.SessionId -> AiChatSession là ON DELETE CASCADE, nên khi DB
//   xoá 1 session thì tất cả message con của nó bị xoá theo ngay ở tầng DB.
// =============================================================================
public class AiChatRetentionJob(
    ApplicationDbContext context,
    IOptions<AiOptions> aiOptions
)
{
    public async Task CleanupOldSessionsAsync()
    {
        var retentionDays = aiOptions.Value.RetentionDays;

        // 0 (hoặc âm) = tắt retention, giữ lịch sử vĩnh viễn
        if (retentionDays <= 0)
            return;

        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

        // ExecuteDeleteAsync (EF Core 7+) sinh 1 câu DELETE ... WHERE chạy thẳng ở DB,
        // KHÔNG load entity nào vào RAM -> nhanh và nhẹ, phù hợp job dọn dẹp.
        await context.AiChatSessions
            .Where(s => s.UpdatedAt < cutoff)
            .ExecuteDeleteAsync();
    }
}
