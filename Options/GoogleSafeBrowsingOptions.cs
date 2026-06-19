namespace NetURLScanner.Options;

public class GoogleSafeBrowsingOptions
{
    public const string SectionName = "GoogleSafeBrowsing";

    public bool Enabled { get; set; }

    public string ApiKey { get; set; } = string.Empty;
}
