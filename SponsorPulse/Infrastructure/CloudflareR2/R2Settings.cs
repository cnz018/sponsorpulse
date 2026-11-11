namespace SponsorPulse.Infrastructure.CloudflareR2;

public class R2Settings
{
    public string AccountId { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string ServiceUrl { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public int ExpirationTimeInMinute { get; set; }
    public Dictionary<string, string> UploadHeaders { get; set; } = [];
}
