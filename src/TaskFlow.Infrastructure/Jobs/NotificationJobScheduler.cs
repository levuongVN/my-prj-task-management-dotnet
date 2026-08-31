using Hangfire;
using Microsoft.Extensions.Hosting;

namespace TaskFlow.Infrastructure.Jobs;

public class NotificationJobScheduler : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        RecurringJob.AddOrUpdate<NotificationJobs>(
            "send-overdue-notifications",
            job => job.SendOverdueNotificationsAsync(),
            "*/10 * * * *"); // This cron expression means the job will run every 10 minutes.
          // */10  *    *   *     *
          // │     │    │   │     │
         // phút  giờ  ngày tháng weekday 
         
         // Nghĩa là sẽ lấy thời gian hiện tại và chia cho 10, nếu dư thì không chạy, nếu không dư thì chạy. 
         // Ví dụ: 10 phút, 20 phút, 30 phút, 40 phút, 50 phút, 60 phút sẽ chạy. 
         // Còn 1 phút, 2 phút, 3 phút, 4 phút, 5 phút, 6 phút, 7 phút, 8 phút, 9 phút sẽ không chạy.
         
        // Scheduler không gọi trực tiếp SendOverdueNotificationsAsync(). Nó:
        // Dò cron → khi thoả mãn, sinh ra 1 "job thật" và lưu vào DB (hangfire.job, state=Enqueued).
        // Worker (từ AddHangfireServer) mới là thằng nhận job từ DB và thực thi method.
        // Khi chạy method đó → nó mới gọi xuống dần NotificationService → NotificationSender (realtime).


        RecurringJob.AddOrUpdate<NotificationJobs>(
            "send-deadline-approaching-notifications",
            job => job.SendDeadlineApproachingNotificationsAsync(2),
            "*/10 * * * *");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
