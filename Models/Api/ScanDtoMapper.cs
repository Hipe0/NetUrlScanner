using NetURLScanner.Models;
using NetURLScanner.Models.Api;

namespace NetURLScanner.Models.Api;

public static class ScanDtoMapper
{
    public static ScanUrlResponse ToResponse(UrlScan scan, bool includeId = true)
    {
        var reasons = string.IsNullOrWhiteSpace(scan.Reasons)
            ? []
            : scan.Reasons
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

        return new ScanUrlResponse
        {
            Id = includeId ? scan.Id : null,
            Url = scan.Url,
            Status = scan.Status,
            StatusCode = scan.StatusCode,
            ResponseTimeMs = scan.ResponseTimeMs,
            IsHttps = scan.IsHttps,
            RiskScore = scan.RiskScore,
            RiskLevel = scan.RiskLevel,
            Reasons = reasons,
            ErrorMessage = scan.ErrorMessage,
            ScannedAt = scan.ScannedAt,
            Geolocation = new GeolocationDto
            {
                IpAddress = scan.IpAddress,
                CountryName = scan.CountryName,
                CountryCode = scan.CountryCode,
                City = scan.City,
                Isp = scan.Isp,
                Latitude = scan.Latitude,
                Longitude = scan.Longitude
            }
        };
    }
}
