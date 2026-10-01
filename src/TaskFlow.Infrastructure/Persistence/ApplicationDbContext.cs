using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Common;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options
    ) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<UserDevice> UserDevices => Set<UserDevice>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<SubtaskItem> Subtasks => Set<SubtaskItem>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<AiChatSession> AiChatSessions => Set<AiChatSession>();
    public DbSet<AiChatMessage> AiChatMessages => Set<AiChatMessage>();

    public override int SaveChanges()
    {
        UpdateAuditTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        UpdateAuditTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateAuditTimestamps()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.Email).IsUnique();

            entity.Property(x => x.Email)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(x => x.FullName)
                .IsRequired()
                .HasMaxLength(255);
        });

        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Title)
                .IsRequired()
                .HasMaxLength(255);

            entity.HasOne(x => x.User)
                .WithMany(x => x.Tasks)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasOne(x => x.User)
                .WithMany(x => x.RefreshTokens)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.UserDevice)
                .WithMany(x => x.RefreshTokens)
                .HasForeignKey(x => x.UserDeviceId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => new { x.UserId, x.UserDeviceId });
        });
        modelBuilder.Entity<UserDevice>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.DeviceFingerprint)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(x => x.DeviceType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.DeviceName)
                .HasMaxLength(100);

            entity.Property(x => x.DeviceToken)
                .HasMaxLength(500);

            entity.HasIndex(x => x.DeviceToken)
                .HasFilter("\"DeviceToken\" IS NOT NULL");

            entity.Property(x => x.IpAddress)
                .HasMaxLength(45);

            entity.HasOne(x => x.User)
                .WithMany(x => x.Devices)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.UserId, x.DeviceFingerprint })
                .IsUnique();
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(x => x.Id);

            // Dedup chống trùng notification (Hangfire job chạy lặp 10 phút).
            // Unique index chỉ áp dụng khi DeduplicationKey KHÁC rỗng (filter)
            // vì notification không có dedup key lưu chuỗi "" -> nếu đánh unique
            // toàn cột sẽ chặn nhiều notification thường có key "".
            entity.HasIndex(x => x.DeduplicationKey)
                .IsUnique()
                .HasFilter("\"DeduplicationKey\" <> ''");

            // Index hỗ trợ query theo user + sắp theo CreatedAt (list notification)
            entity.HasIndex(x => new { x.UserId, x.CreatedAt });

            entity.HasOne(x => x.User)
                .WithMany(x => x.Notifications)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Comment>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Content)
                .IsRequired()
                .HasMaxLength(2000);

            entity.HasIndex(x => new { x.TaskId, x.CreatedAt });

            entity.HasOne(x => x.Task)
                .WithMany(x => x.Comments)
                .HasForeignKey(x => x.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Author)
                .WithMany(x => x.Comments)
                .HasForeignKey(x => x.AuthorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SubtaskItem>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Title)
                .IsRequired()
                .HasMaxLength(255);

            // Index hỗ trợ query subtask theo task + sắp theo Position (checklist)
            entity.HasIndex(x => new { x.TaskId, x.Position });

            entity.HasOne(x => x.Task)
                .WithMany(x => x.Subtasks)
                .HasForeignKey(x => x.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasKey(x => x.Id);

            // Lookup hash token khi reset password: unique index giúp query O(1)
            entity.Property(x => x.TokenHash)
                .IsRequired()
                .HasMaxLength(128);

            entity.HasIndex(x => x.TokenHash)
                .IsUnique();

            entity.HasOne(x => x.User)
                .WithMany(x => x.PasswordResetTokens)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmailVerificationToken>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.TokenHash)
                .IsRequired()
                .HasMaxLength(128);

            entity.HasIndex(x => x.TokenHash)
                .IsUnique();

            entity.HasOne(x => x.User)
                .WithMany(x => x.EmailVerificationTokens)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Label>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Color)
                .HasMaxLength(9);

            // 1 user không có 2 label trùng tên
            entity.HasIndex(x => new { x.UserId, x.Name })
                .IsUnique();
        });

        // Many-to-many: join table tên rõ ràng + index LabelId cho filter ?labelId
        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.HasMany(x => x.Labels)
                .WithMany(x => x.Tasks)
                .UsingEntity("TaskLabels",
                    j => j.HasIndex("LabelsId").HasDatabaseName("IX_TaskLabels_LabelsId"));
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasMany(x => x.Labels)
                .WithMany(x => x.Projects)
                .UsingEntity("ProjectLabels",
                    j => j.HasIndex("LabelsId").HasDatabaseName("IX_ProjectLabels_LabelsId"));
        });

        modelBuilder.Entity<AiChatSession>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Title)
                .IsRequired()
                .HasMaxLength(255);

            // Index hỗ trợ query "list session của user" + sắp theo lần cập nhật gần nhất
            // (mỗi khi có tin nhắn mới, service gọi TouchAsync để bump UpdatedAt)
            entity.HasIndex(x => new { x.UserId, x.UpdatedAt });

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AiChatMessage>(entity =>
        {
            entity.HasKey(x => x.Id);

            // Nội dung chat có thể dài (prompt lẫn câu trả lời của AI) nên không
            // giới hạn max length; service đã chặn input <= 4000 ký tự
            entity.Property(x => x.Content)
                .IsRequired();

            // Index hỗ trợ load lịch sử hội thoại theo session + sắp theo thời gian
            entity.HasIndex(x => new { x.SessionId, x.CreatedAt });

            // Xoá session là xoá luôn toàn bộ message (DB cascade) - cũng chính là
            // cơ chế dọn dẹp của AiChatRetentionJob khi session quá RetentionDays
            entity.HasOne(x => x.Session)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}