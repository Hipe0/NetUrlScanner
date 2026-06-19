namespace NetURLScanner.Helpers;

public static class DomainHelper
{
    private static readonly string[] VietnameseSecondLevelTlds =
    {
        ".com.vn", ".net.vn", ".org.vn", ".gov.vn", ".edu.vn",
        ".biz.vn", ".info.vn", ".name.vn", ".pro.vn", ".health.vn"
    };

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

    public static string? NormalizeHost(string host)
    {
        host = host.Trim().ToLowerInvariant().TrimEnd('.');
        if (host.StartsWith("www."))
            host = host[4..];

        return string.IsNullOrWhiteSpace(host) ? null : host;
    }

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
