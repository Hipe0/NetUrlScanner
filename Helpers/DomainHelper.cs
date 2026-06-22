namespace NetURLScanner.Helpers;

/// <summary>
/// Tiện ích chuẩn hóa tên miền — dùng chung cho quét URL, vote domain, blacklist.
/// </summary>
public static class DomainHelper
{
    private static readonly string[] VietnameseSecondLevelTlds =
    {
        ".com.vn", ".net.vn", ".org.vn", ".gov.vn", ".edu.vn",
        ".biz.vn", ".info.vn", ".name.vn", ".pro.vn", ".health.vn"
    };

    /// <summary>
    /// Nhận URL đầy đủ hoặc hostname → trả host đã chuẩn hóa (lowercase, bỏ www).
    /// </summary>
    public static string? NormalizeDomain(string? urlOrHost)
    {
        if (string.IsNullOrWhiteSpace(urlOrHost)) return null;

        var input = urlOrHost.Trim().ToLowerInvariant();

        if (!input.Contains("://") && !input.StartsWith("//"))
        {
            if (input.Contains('/'))
                input = "https://" + input.Split('/')[0];
            else
                input = "https://" + input;
        }

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
            return null;

        return NormalizeHost(uri.Host);
    }

    /// <summary>Chuẩn hóa hostname: trim, lowercase, bỏ tiền tố www.</summary>
    public static string? NormalizeHost(string host)
    {
        host = host.Trim().ToLowerInvariant().TrimEnd('.');
        if (host.StartsWith("www."))
            host = host[4..];

        return string.IsNullOrWhiteSpace(host) ? null : host;
    }

    /// <summary>
    /// Trích "domain đăng ký" — vd. sub.shop.vietcombank.com.vn → vietcombank.com.vn.
    /// Hỗ trợ TLD hai cấp của Việt Nam (.com.vn).
    /// </summary>
    public static string GetRegistrableDomain(string host)
    {
        host = NormalizeHost(host) ?? host;

        foreach (var tld in VietnameseSecondLevelTlds)
        {
            if (!host.EndsWith(tld)) continue;
            var withoutTld = host[..^tld.Length];
            var labels = withoutTld.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (labels.Length == 0) return host;
            return $"{labels[^1]}{tld}";
        }

        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return host;
        return $"{parts[^2]}.{parts[^1]}";
    }
}
