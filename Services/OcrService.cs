using Microsoft.Extensions.Options;
using NetURLScanner.Options;
using Tesseract;

namespace NetURLScanner.Services;

/// <summary>Tesseract OCR — cần thư mục tessdata/*.traineddata trong repo.</summary>
public class OcrResult
{
    public bool Success { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public class OcrService
{
    private readonly OcrOptions _options;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<OcrService> _logger;

    public OcrService(IOptions<OcrOptions> options, IWebHostEnvironment env, ILogger<OcrService> logger)
    {
        _options = options.Value;
        _env = env;
        _logger = logger;
    }

    public string GetUploadDirectory()
    {
        var dir = Path.Combine(_env.WebRootPath, "uploads", _options.UploadFolder);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public async Task<string> SaveUploadAsync(IFormFile file, int? userId, CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".bmp" };
        if (!allowed.Contains(ext))
            throw new InvalidOperationException("Chỉ chấp nhận ảnh JPG, PNG, WEBP, BMP.");

        if (file.Length > _options.MaxUploadBytes)
            throw new InvalidOperationException($"Ảnh tối đa {_options.MaxUploadBytes / 1024 / 1024}MB.");

        var dir = GetUploadDirectory();
        var fileName = $"ocr_{userId ?? 0}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{ext}";
        var physicalPath = Path.Combine(dir, fileName);

        await using var stream = new FileStream(physicalPath, FileMode.Create);
        await file.CopyToAsync(stream, cancellationToken);

        return $"/uploads/{_options.UploadFolder}/{fileName}";
    }

    public OcrResult ExtractText(string physicalImagePath)
    {
        var tessDataPath = ResolveTessDataPath();
        if (tessDataPath == null)
        {
            return new OcrResult
            {
                Success = false,
                ErrorMessage = "Chưa có dữ liệu Tesseract (tessdata). Xem README phần OCR."
            };
        }

        try
        {
            using var engine = new TesseractEngine(tessDataPath, _options.Languages, EngineMode.Default);
            using var img = Pix.LoadFromFile(physicalImagePath);
            using var page = engine.Process(img);
            var text = page.GetText()?.Trim() ?? string.Empty;

            return new OcrResult
            {
                Success = true,
                Text = text
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OCR failed for {Path}", physicalImagePath);
            return new OcrResult
            {
                Success = false,
                ErrorMessage = "Không đọc được chữ từ ảnh. Thử ảnh rõ hơn hoặc kiểm tra tessdata."
            };
        }
    }

    private string? ResolveTessDataPath()
    {
        var candidates = new[]
        {
            Path.Combine(_env.ContentRootPath, _options.TessDataFolder),
            Path.Combine(AppContext.BaseDirectory, _options.TessDataFolder),
            Path.Combine(Directory.GetCurrentDirectory(), _options.TessDataFolder)
        };

        foreach (var path in candidates)
        {
            if (Directory.Exists(path) && Directory.GetFiles(path, "*.traineddata").Length > 0)
                return path;
        }

        return null;
    }
}
