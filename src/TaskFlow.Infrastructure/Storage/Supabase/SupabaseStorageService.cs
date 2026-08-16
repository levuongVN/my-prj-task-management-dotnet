using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

public class SupabaseStorageService
    : IFileStorageService
{
    private readonly IAmazonS3 _s3;

    private readonly SupabaseStorageOptions _options;

    public SupabaseStorageService(
        IAmazonS3 s3,
        IOptions<SupabaseStorageOptions> options
    )
    {
        _s3 = s3;
        _options = options.Value;
    }

    public async Task<string> UploadAvatarAsync(
        Guid userId,
        FileUploadDto file,
        CancellationToken cancellationToken = default
    )
    {
        var path = $"users/{userId}/avatar";

        var request = new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = path,
            InputStream = file.Stream,
            ContentType = file.ContentType
        };

        await _s3.PutObjectAsync(
            request,
            cancellationToken
        );

        return path;
    }

    public async Task<string> CreateSignedUrlAsync(
        string path,
        CancellationToken cancellationToken = default
    )
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = path,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(10)
        };

        return await _s3.GetPreSignedURLAsync(request);
    }

    public async Task DeleteAsync(
        string path,
        CancellationToken cancellationToken = default
    )
    {
        await _s3.DeleteObjectAsync(
            _options.Bucket,
            path,
            cancellationToken
        );
    }
}