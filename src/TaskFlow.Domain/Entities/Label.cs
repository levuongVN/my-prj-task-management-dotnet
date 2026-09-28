using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities;

// Nhãn phân loại tự do do mỗi user tự định nghĩa (name + màu hiển thị),
// khác với Status/Priority là chiều cố định dùng chung hệ thống.
// Task/Project chỉ giữ tham chiếu qua join table -> đổi tên/màu ở Label
// thì mọi chỗ hiển thị tự cập nhật, xóa Label thì join rows cascade đi theo.
public class Label : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    // Hex #RRGGBB - client render chip màu trực tiếp
    public string Color { get; set; } = "#2563eb";

    public Guid UserId { get; set; }

    // Navigation Property
    public User User { get; set; } = null!;
    public ICollection<TaskItem> Tasks { get; set; } = [];
    public ICollection<Project> Projects { get; set; } = [];
}
