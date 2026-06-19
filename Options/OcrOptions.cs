namespace NetURLScanner.Options;

public class OcrOptions
{
    public const string SectionName = "Ocr";

    /// <summary>Thư mục con trong wwwroot/uploads/ để lưu ảnh OCR.</summary>
    public string UploadFolder { get; set; } = "ocr-images";

    public string TessDataFolder { get; set; } = "tessdata";

    public string Languages { get; set; } = "eng+vie";

    public int MaxUploadBytes { get; set; } = 5 * 1024 * 1024;
}
