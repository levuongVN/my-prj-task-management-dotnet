namespace TaskFlow.Application.Common;

public class PagedResult<T>
{
    public const int DefaultPageSize = 20;

    // Chặn page size quá lớn (client gửi 10.000 -> server vẫn chỉ trả tối đa 100)
    public const int MaxPageSize = 100;

    public List<T> Items { get; set; } = [];

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages =>
        PageSize <= 0
            ? 0
            : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasNext => Page < TotalPages;

    public bool HasPrevious => Page > 1;

    // Clamp input trước khi dùng: page bắt đầu từ 1, pageSize trong 1..100
    public static (int Page, int PageSize) Normalize(int page, int pageSize)
    {
        return (
            Math.Max(1, page),
            Math.Clamp(pageSize, 1, MaxPageSize)
        );
    }
}
