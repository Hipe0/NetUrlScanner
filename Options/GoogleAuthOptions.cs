namespace NetURLScanner.Options;

public class GoogleAuthOptions
{
    public const string SectionName = "GoogleAuth";

    public bool Enabled { get; set; }

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;
}
