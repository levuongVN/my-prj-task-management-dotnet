public class SupabaseStorageOptions
{
    public string Endpoint { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public string AccessKeyId { get; set; } = string.Empty;

    public string SecretAccessKey { get; set; } = string.Empty;

    public string Bucket { get; set; } = "avatars";
}