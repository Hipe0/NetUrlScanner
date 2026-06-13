using System.ComponentModel.DataAnnotations;

namespace NetURLScanner.Models.Api;

public class ScanUrlRequest
{
    [Required(ErrorMessage = "URL là bắt buộc.")]
    [StringLength(2048, ErrorMessage = "URL không được vượt quá 2048 ký tự.")]
    public string Url { get; set; } = string.Empty;

    /// <summary>Lưu kết quả vào database. Mặc định: true.</summary>
    public bool SaveToHistory { get; set; } = true;
}

public class ScanUrlResponse
{
    public int? Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? StatusCode { get; set; }
    public long ResponseTimeMs { get; set; }
    public bool IsHttps { get; set; }
    public int RiskScore { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public List<string> Reasons { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public DateTime ScannedAt { get; set; }
    public GeolocationDto? Geolocation { get; set; }
}

public class GeolocationDto
{
    public string? IpAddress { get; set; }
    public string? CountryName { get; set; }
    public string? CountryCode { get; set; }
    public string? City { get; set; }
    public string? Isp { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public class ScanStatsResponse
{
    public int TotalScans { get; set; }
    public int SafeCount { get; set; }
    public int WarningCount { get; set; }
    public int SuspiciousCount { get; set; }
    public int OnlineCount { get; set; }
    public int OfflineCount { get; set; }
}
