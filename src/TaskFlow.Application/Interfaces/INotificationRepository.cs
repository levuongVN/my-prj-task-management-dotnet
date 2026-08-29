using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Interfaces;

public interface INotificationRepository
{
    // Thêm một thông báo mới vào DB
    void Add(Notification notification);
    
    // Lấy các thông báo chưa đọc của 1 User, sắp xếp mới nhất lên đầu
    Task<List<Notification>> GetUnreadByUserIdAsync(Guid userId);
    
    // Đếm số lượng chưa đọc (để hiển thị con số đỏ ở chuông)
    Task<int> GetUnreadCountAsync(Guid userId);

    // Lấy 1 thông báo cụ thể (để cập nhật trạng thái đã đọc)
    Task<Notification?> GetByIdAsync(Guid id);
    
    // Lưu các thay đổi xuống DB
    Task SaveChangesAsync();
}