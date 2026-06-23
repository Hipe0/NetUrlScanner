using SkiaSharp;
using ZXing.SkiaSharp;

namespace NetURLScanner.Services;

public class QrDecodeResult
{
    public bool Success { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

/// <summary>Đọc mã QR từ ảnh upload — ZXing decode, lưu file trong wwwroot/uploads/qr-images/.</summary>
public class QrScanService
{
    private const string UploadFolder = "qr-images";

    private readonly IWebHostEnvironment _env;

    public QrScanService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task<string> SaveUploadAsync(IFormFile file, int? userId, CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".bmp" };
        if (!allowed.Contains(ext))
            throw new InvalidOperationException("Chỉ chấp nhận ảnh JPG, PNG, WEBP, BMP.");

        if (file.Length > 5 * 1024 * 1024)
            throw new InvalidOperationException("Ảnh tối đa 5MB.");

        var dir = Path.Combine(_env.WebRootPath, "uploads", UploadFolder);
        Directory.CreateDirectory(dir);

        var fileName = $"qr_{userId ?? 0}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{ext}";
        var physicalPath = Path.Combine(dir, fileName);

        await using var stream = new FileStream(physicalPath, FileMode.Create);
        await file.CopyToAsync(stream, cancellationToken);

        return $"/uploads/{UploadFolder}/{fileName}";
    }

    public QrDecodeResult DecodeFromFile(string physicalPath)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(physicalPath);
            if (bitmap == null)
            {
                return new QrDecodeResult
                {
                    Success = false,
                    ErrorMessage = "Không đọc được file ảnh."
                };
            }

            var reader = new BarcodeReader();
            var result = reader.Decode(bitmap);
            var text = result?.Text?.Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                return new QrDecodeResult
                {
                    Success = false,
                    ErrorMessage = "Không tìm thấy mã QR trong ảnh."
                };
            }

            return new QrDecodeResult { Success = true, Text = text };
        }
        catch (Exception)
        {
            return new QrDecodeResult
            {
                Success = false,
                ErrorMessage = "Không đọc được mã QR. Thử ảnh rõ hơn hoặc căn thẳng mã QR."
            };
        }
    }
}
