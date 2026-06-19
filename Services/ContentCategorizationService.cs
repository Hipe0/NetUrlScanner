using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace NetURLScanner.Services;

public class CategoryResult
{
    public string PrimaryCategory { get; set; } = "Chưa phân loại";
    public List<string> Tags { get; set; } = new();
}

public class ContentCategorizationService
{
    private static readonly Dictionary<string, string[]> CategoryKeywords = new()
    {
        ["Tin tức"] = ["tin tức", "tin tuc", "báo", "bao", "thời sự", "thoi su", "news", "headline", "phóng sự", "journal"],
        ["Thương mại điện tử"] = ["mua hàng", "mua hang", "giá", "gia", "sản phẩm", "san pham", "shop", "cart", "checkout", "đặt hàng", "dat hang", "store", "ecommerce", "shopee", "lazada", "tiki"],
        ["Ngân hàng / Tài chính"] = ["ngân hàng", "ngan hang", "bank", "tài khoản", "tai khoan", "vay", "đầu tư", "dau tu", "chứng khoán", "chung khoan", "thanh toán", "thanh toan", "finance", "wallet", "crypto"],
        ["Cờ bạc"] = ["casino", "cá cược", "ca cuoc", "nhà cái", "nha cai", "bet", "betting", "slot", "poker", "gambling", "jackpot", "roulette", "baccarat", "taixiu", "xoc dia"],
        ["Giáo dục"] = ["học", "hoc", "trường", "truong", "giáo dục", "giao duc", "university", "college", "khóa học", "khoa hoc", "education", "student", "đại học", "dai hoc"],
        ["Công nghệ"] = ["phần mềm", "phan mem", "software", "developer", "api", "github", "công nghệ", "cong nghe", "technology", "programming", "cloud", "server", "hosting"],
        ["Giải trí"] = ["phim", "game", "music", "giải trí", "giai tri", "video", "entertainment", "trailer", "playlist", "anime", "streaming"]
    };

    public CategoryResult Categorize(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        foreach (var node in doc.DocumentNode.SelectNodes("//script|//style|//noscript") ?? Enumerable.Empty<HtmlNode>())
            node.Remove();

        var title = HtmlEntity.DeEntitize(doc.DocumentNode.SelectSingleNode("//title")?.InnerText ?? "");
        var metaDesc = doc.DocumentNode
            .SelectSingleNode("//meta[translate(@name,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz')='description']")
            ?.GetAttributeValue("content", "") ?? "";
        var bodyText = HtmlEntity.DeEntitize(doc.DocumentNode.SelectSingleNode("//body")?.InnerText ?? "");

        var text = Regex.Replace($"{title} {metaDesc} {bodyText}".ToLowerInvariant(), @"\s+", " ");
        if (text.Length > 8000)
            text = text[..8000];

        var scores = new Dictionary<string, int>();
        foreach (var (category, keywords) in CategoryKeywords)
        {
            var score = keywords.Sum(kw => CountOccurrences(text, kw));
            if (score > 0)
                scores[category] = score;
        }

        if (scores.Count == 0)
        {
            return new CategoryResult
            {
                PrimaryCategory = "Tổng quát",
                Tags = new List<string> { "Tổng quát" }
            };
        }

        var primary = scores.OrderByDescending(x => x.Value).First().Key;
        var tags = scores
            .Where(x => x.Value >= Math.Max(2, scores.Values.Max() / 2))
            .OrderByDescending(x => x.Value)
            .Select(x => x.Key)
            .Take(3)
            .ToList();

        if (!tags.Contains(primary))
            tags.Insert(0, primary);

        return new CategoryResult
        {
            PrimaryCategory = primary,
            Tags = tags
        };
    }

    private static int CountOccurrences(string text, string keyword)
    {
        if (string.IsNullOrEmpty(keyword)) return 0;
        int count = 0, index = 0;
        while ((index = text.IndexOf(keyword, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += keyword.Length;
        }
        return count;
    }
}
