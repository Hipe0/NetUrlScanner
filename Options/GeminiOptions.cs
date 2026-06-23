namespace NetURLScanner.Options;

public class GeminiOptions
{
    public const string SectionName = "Gemini";
    public const string DefaultModel = "gemini-3.1-flash-lite";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = DefaultModel;

    /// <summary>minimal | low | medium | high — chỉ với model không phải *-lite (vd. gemini-3.5-flash).</summary>
    public string ThinkingLevel { get; set; } = "";

    public int MaxOutputTokens { get; set; } = 512;
}
