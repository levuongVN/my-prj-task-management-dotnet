public class FileUploadDto
{
    public Stream Stream { get; init; } = Stream.Null;

    public string FileName { get; init; } = string.Empty;

    public string ContentType { get; init; } = string.Empty;

    public long Length { get; init; }
}