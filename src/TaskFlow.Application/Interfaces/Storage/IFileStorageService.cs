public interface IFileStorageService
{
    Task<string> UploadAvatarAsync(
        Guid userId,
        FileUploadDto file,
        CancellationToken cancellationToken = default
    );

    Task<string> CreateSignedUrlAsync(
        string path,
        CancellationToken cancellationToken = default
    );

    Task DeleteAsync(
        string path,
        CancellationToken cancellationToken = default
    );
}