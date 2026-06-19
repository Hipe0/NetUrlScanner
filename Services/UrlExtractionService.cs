using System.Text.RegularExpressions;

namespace NetURLScanner.Services;

public class UrlExtractionService
{
    private static readonly Regex UrlRegex = new(
        @"(?i)\b((?:https?://|www\.)[^\s<>""']+|[a-z0-9](?:[a-z0-9\-]{0,61}[a-z0-9])?(?:\.[a-z0-9](?:[a-z0-9\-]{0,61}[a-z0-9])?)+(?:/[^\s<>""']*)?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public List<string> ExtractUrls(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<string>();

        var normalized = text
            .Replace("http//", "http://", StringComparison.OrdinalIgnoreCase)
            .Replace("https//", "https://", StringComparison.OrdinalIgnoreCase)
            .Replace("hxxp://", "http://", StringComparison.OrdinalIgnoreCase)
            .Replace("hxxps://", "https://", StringComparison.OrdinalIgnoreCase);

        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in UrlRegex.Matches(normalized))
        {
            var raw = match.Groups[1].Value.Trim().TrimEnd('.', ',', ';', ':', ')', ']', '}', '!', '?');
            if (string.IsNullOrWhiteSpace(raw)) continue;

            var candidate = raw.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                ? "https://" + raw
                : raw;

            if (!candidate.Contains('.') && !candidate.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!candidate.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                candidate = "https://" + candidate;

            if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                results.Add(uri.ToString());
            }
        }

        return results.Take(10).ToList();
    }
}
