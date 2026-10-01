namespace TaskFlow.Application.Common;

// Cấu hình cho tính năng AI, bind từ section "Ai" trong appsettings.json.
// Ở Infrastructure: services.Configure<AiOptions>(configuration.GetSection("Ai")).
// Ở AiService: inject IOptions<AiOptions> rồi đọc .Value.
// Đặt ở Application vì cả Application (quota, history window) lẫn Infrastructure
// (ApiKey, Model, retention) đều cần đọc; Infrastructure đã reference Application.
public class AiOptions
{
    // Lấy free tại https://aistudio.google.com/apikey - KHÔNG commit key thật
    public string ApiKey { get; set; } = string.Empty;

    // Model Gemini dùng để chat/parse. "gemini-flash-latest" là alias Google
    // luôn trỏ tới bản Flash mới nhất - tránh lỗi "model no longer available
    // to new users" khi Google khai tử model cũ (từng bị với gemini-2.5-flash).
    public string Model { get; set; } = "gemini-flash-latest";

    // Danh sách model dự phòng, thử lần lượt khi model chính gặp lỗi tạm thời
    // (503 high demand / 429 hết quota). Để trống = chỉ dùng model chính.
    public List<string> FallbackModels { get; set; } = new();

    // Số lượt AI tối đa / user / ngày (bảo vệ quota free tier dùng chung).
    // <= 0 = tắt giới hạn.
    public int DailyUserLimit { get; set; } = 80;

    // Số ngày giữ lại lịch sử chat. Job dọn dẹp xoá session cũ hơn ngưỡng này.
    // 0 = giữ vĩnh viễn.
    public int RetentionDays { get; set; } = 30;
}
