namespace TaskFlow.Domain.Enums;

public enum RecurrenceType
{
    None = 0,

    // Deadline mới = deadline cũ + 1 ngày
    Daily = 1,

    // Deadline mới = deadline cũ + 7 ngày
    Weekly = 2,

    // Deadline mới = deadline cũ + 1 tháng
    // (31/01 -> .NET AddMonths tự clamp về 28/02)
    Monthly = 3
}
