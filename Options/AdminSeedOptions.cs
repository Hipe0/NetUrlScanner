namespace NetURLScanner.Options;

public class AdminSeedOptions
{
    public const string SectionName = "AdminSeed";

    public bool Enabled { get; set; } = true;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
