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
    }
}
