using System.ComponentModel.DataAnnotations;

namespace NetURLScanner.Models
{
    public class UrlScan
    {
        public int Id { get; set; }

        [Required]
        [Url]
        public string Url { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;
        public int? StatusCode { get; set; }
        public long ResponseTimeMs { get; set; }

        public bool IsHttps { get; set; }

        public int RiskScore { get; set; }
        public string RiskLevel { get; set; } = string.Empty;

        public string Reasons { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }

        public string? IpAddress { get; set; }
        public string? CountryName { get; set; }
        public string? CountryCode { get; set; }
        public string? City { get; set; }
        public string? Isp { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public DateTime ScannedAt { get; set; } = DateTime.Now;

        [MaxLength(50)]
        [Display(Name = "Nhãn")]
        public string? UserLabel { get; set; }

        [MaxLength(500)]
        [Display(Name = "Ghi chú")]
        public string? UserNotes { get; set; }

        [MaxLength(255)]
        public string? NormalizedDomain { get; set; }

        [MaxLength(80)]
        public string? SiteCategory { get; set; }

        [MaxLength(200)]
        public string? SiteCategoryTags { get; set; }

        [MaxLength(30)]
        public string? SafeBrowsingStatus { get; set; }

        [MaxLength(80)]
        public string? SafeBrowsingThreatType { get; set; }

        public int? UserId { get; set; }
        public User? User { get; set; }
    }
}