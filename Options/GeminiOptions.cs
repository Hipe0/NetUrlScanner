namespace NetURLScanner.Options;

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>gemini-3.1-flash-lite = nhanh, rẻ. gemini-3.5-flash = thông minh hơn nhưng chậm hơn.</summary>
    public string Model { get; set; } = "gemini-3.1-flash-lite";

  /// <summary>minimal | low | medium | high — minimal/low giảm độ trễ cho câu hỏi đơn giản.</summary>
    public string ThinkingLevel { get; set; } = "minimal";

    public int MaxOutputTokens { get; set; } = 512;
}
