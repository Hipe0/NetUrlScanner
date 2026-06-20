using System.Text.Json;
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
    private const int MinPrimaryScore = 5;
    private const int MinMargin = 3;

    private static readonly Dictionary<string, string> DomainOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["udemy.com"] = "Giáo dục / Khóa học",
        ["coursera.org"] = "Giáo dục / Khóa học",
        ["edx.org"] = "Giáo dục / Khóa học",
        ["khanacademy.org"] = "Giáo dục / Khóa học",
        ["skillshare.com"] = "Giáo dục / Khóa học",
        ["fifaaddict.com"] = "Game",
        ["futbin.com"] = "Game",
        ["futhead.com"] = "Game",
        ["shopee.vn"] = "Thương mại điện tử",
        ["lazada.vn"] = "Thương mại điện tử",
        ["tiki.vn"] = "Thương mại điện tử",
        ["amazon.com"] = "Thương mại điện tử",
        ["github.com"] = "Công nghệ",
        ["stackoverflow.com"] = "Công nghệ",
        ["vnexpress.net"] = "Tin tức",
        ["dantri.com.vn"] = "Tin tức",
        ["tuoitre.vn"] = "Tin tức",
        ["thanhnien.vn"] = "Tin tức"
    };

    private static readonly (Regex pattern, string category, int boost)[] UrlHints =
    [
        (new Regex(@"udemy\.com|coursera\.org|edx\.org|khanacademy|skillshare|/course[s]?/|/learn/|/academy/|/khoa-hoc|/bai-giang", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Giáo dục / Khóa học", 45),
        (new Regex(@"fifa|fc-?online|ea-?sports|fc2[45]|pes|futbin|futhead|/game[s]?/|esports", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Game", 40),
        (new Regex(@"steam\.com|epicgames|roblox|minecraft|playstation|xbox", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Game", 30),
        (new Regex(@"shopee|lazada|tiki\.vn|amazon\.|ebay\.|/cart|/checkout|/san-pham|/product[s]?/", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Thương mại điện tử", 25),
        (new Regex(@"github\.com|stackoverflow|npmjs|/docs/|/api/", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Công nghệ", 20),
        (new Regex(@"/tin-tuc|/news/|/bao-|vnexpress|dantri|tuoitre", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Tin tức", 20),
        (new Regex(@"casino|betting|cá cược|ca-cuoc|nha-cai", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Cờ bạc", 35)
    ];

    private static readonly Dictionary<string, (string phrase, int weight, bool wordBoundary)[]> CategoryKeywords = new()
    {
        ["Tin tức"] =
        [
            ("tin tức", 4, false), ("tin tuc", 4, false), ("thời sự", 4, false), ("thoi su", 4, false),
            ("bản tin", 3, false), ("headline", 3, true), ("breaking news", 4, false),
            ("phóng sự", 3, false), ("journalist", 3, true), ("đọc báo", 3, false)
        ],
        ["Thương mại điện tử"] =
        [
            ("add to cart", 5, false), ("giỏ hàng", 5, false), ("gio hang", 5, false),
            ("checkout", 5, true), ("đặt hàng", 5, false), ("dat hang", 5, false),
            ("mua ngay", 4, false), ("shop now", 4, false), ("order now", 4, false),
            ("freeship", 3, true), ("free ship", 3, false), ("thanh toán khi nhận", 4, false),
            ("giá bán", 3, false), ("gia ban", 3, false), ("mã giảm giá", 3, false)
        ],
        ["Ngân hàng / Tài chính"] =
        [
            ("ngân hàng", 4, false), ("ngan hang", 4, false), ("internet banking", 5, false),
            ("mobile banking", 5, false), ("tài khoản ngân hàng", 5, false),
            ("chuyển khoản", 4, false), ("chuyen khoan", 4, false),
            ("vay vốn", 4, false), ("chứng khoán", 4, false), ("chung khoan", 4, false),
            ("đầu tư", 3, false), ("dau tu", 3, false), ("crypto wallet", 4, false),
            ("thẻ tín dụng", 4, false), ("lãi suất", 3, false)
        ],
        ["Cờ bạc"] =
        [
            ("casino", 5, true), ("cá cược", 5, false), ("ca cuoc", 5, false),
            ("nhà cái", 5, false), ("nha cai", 5, false), ("betting", 5, true),
            ("slot machine", 5, false), ("jackpot", 4, true), ("roulette", 4, true),
            ("baccarat", 4, true), ("taixiu", 4, true), ("xóc đĩa", 4, false), ("xoc dia", 4, false)
        ],
        ["Giáo dục / Khóa học"] =
        [
            ("khóa học", 5, false), ("khoa hoc", 5, false), ("online course", 5, false),
            ("e-learning", 4, false), ("bài giảng", 4, false), ("bai giang", 4, false),
            ("instructor", 4, true), ("certificate", 3, true), ("đại học", 4, false),
            ("dai hoc", 4, false), ("university", 3, true), ("enroll now", 4, false),
            ("học trực tuyến", 4, false), ("hoc truc tuyen", 4, false),
            ("lesson", 3, true), ("curriculum", 3, true), ("syllabus", 3, true)
        ],
        ["Game"] =
        [
            ("video game", 5, false), ("fc online", 5, false), ("squad builder", 5, false),
            ("đội hình", 4, false), ("doi hinh", 4, false), ("esports", 4, true),
            ("playstation", 4, true), ("xbox", 4, true), ("minecraft", 4, true),
            ("roblox", 4, true), ("league of legends", 5, false), ("valorant", 4, true),
            ("build team", 4, false), ("gameplay", 4, true), ("multiplayer", 3, true),
            ("game", 2, true), ("gaming", 3, true), ("fifa", 4, true), ("fo4", 4, true)
        ],
        ["Công nghệ"] =
        [
            ("phần mềm", 4, false), ("phan mem", 4, false), ("open source", 4, false),
            ("api documentation", 5, false), ("programming", 4, true), ("developer", 3, true),
            ("devops", 4, true), ("repository", 3, true), ("pull request", 4, false),
            ("software development", 5, false), ("cloud hosting", 4, false),
            ("source code", 4, false), ("npm package", 4, false)
        ],
        ["Giải trí"] =
        [
            ("xem phim", 4, false), ("phim hay", 3, false), ("trailer", 3, true),
            ("playlist", 3, true), ("anime", 4, true), ("streaming", 3, true),
            ("netflix", 4, true), ("spotify", 4, true), ("tập phim", 3, false),
            ("music video", 4, false), ("video clip", 3, false)
        ]
    };

    private static readonly Dictionary<string, string> SchemaTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Course"] = "Giáo dục / Khóa học",
        ["Product"] = "Thương mại điện tử",
        ["NewsArticle"] = "Tin tức",
        ["Article"] = "Tin tức",
        ["VideoGame"] = "Game",
        ["Game"] = "Game",
        ["FinancialProduct"] = "Ngân hàng / Tài chính",
        ["BankAccount"] = "Ngân hàng / Tài chính"
    };

    public CategoryResult Categorize(string html, string? pageUrl = null)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        foreach (var node in doc.DocumentNode.SelectNodes("//script|//style|//noscript") ?? Enumerable.Empty<HtmlNode>())
            node.Remove();

        var ctx = ExtractContext(doc, pageUrl);

        if (!string.IsNullOrEmpty(ctx.Host) && TryDomainOverride(ctx.Host, out var overrideCategory))
            return BuildResult(overrideCategory, new Dictionary<string, int> { [overrideCategory] = 100 });

        var scores = ScoreKeywords(ctx);
        ApplyUrlHints(ctx.FullUrl, scores);
        ApplySchemaSignals(ctx.SchemaTypes, scores);
        ApplyOgTypeSignal(ctx.OgType, scores);
        ApplyEduTldBoost(ctx.Host, scores);
        ApplyConflictResolution(ctx, scores);

        return PickResult(scores);
    }

    private static PageContext ExtractContext(HtmlDocument doc, string? pageUrl)
    {
        string host = "", path = "", fullUrl = pageUrl ?? "";
        if (Uri.TryCreate(pageUrl, UriKind.Absolute, out var uri))
        {
            host = uri.Host.ToLowerInvariant();
            if (host.StartsWith("www.")) host = host[4..];
            path = uri.AbsolutePath.ToLowerInvariant();
            fullUrl = uri.ToString();
        }

        var title = CleanText(doc.DocumentNode.SelectSingleNode("//title")?.InnerText);
        var metaDesc = CleanText(GetMetaContent(doc, "description"));
        var ogTitle = CleanText(GetMetaProperty(doc, "og:title"));
        var ogDesc = CleanText(GetMetaProperty(doc, "og:description"));
        var ogType = CleanText(GetMetaProperty(doc, "og:type")).ToLowerInvariant();
        var h1 = CleanText(doc.DocumentNode.SelectSingleNode("//h1")?.InnerText);
        var body = CleanText(doc.DocumentNode.SelectSingleNode("//body")?.InnerText);
        if (body.Length > 5000) body = body[..5000];

        return new PageContext
        {
            Host = host,
            Path = path,
            FullUrl = fullUrl.ToLowerInvariant(),
            Title = title,
            MetaDesc = metaDesc,
            OgTitle = ogTitle,
            OgDesc = ogDesc,
            OgType = ogType,
            H1 = h1,
            Body = body,
            SchemaTypes = ExtractSchemaTypes(doc)
        };
    }

    private static Dictionary<string, int> ScoreKeywords(PageContext ctx)
    {
        var scores = new Dictionary<string, int>();

        foreach (var (category, keywords) in CategoryKeywords)
        {
            var total = 0;
            foreach (var (phrase, weight, wordBoundary) in keywords)
            {
                total += CountWeighted(ctx.Title, phrase, weight * 5, wordBoundary);
                total += CountWeighted(ctx.H1, phrase, weight * 4, wordBoundary);
                total += CountWeighted(ctx.MetaDesc, phrase, weight * 3, wordBoundary);
                total += CountWeighted(ctx.OgTitle, phrase, weight * 3, wordBoundary);
                total += CountWeighted(ctx.OgDesc, phrase, weight * 3, wordBoundary);
                total += CountWeighted(ctx.Body, phrase, weight, wordBoundary);
            }
            if (total > 0) scores[category] = total;
        }

        return scores;
    }

    private static void ApplyUrlHints(string url, Dictionary<string, int> scores)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        foreach (var (pattern, category, boost) in UrlHints)
        {
            if (pattern.IsMatch(url))
                scores[category] = scores.GetValueOrDefault(category) + boost;
        }
    }

    private static void ApplySchemaSignals(List<string> schemaTypes, Dictionary<string, int> scores)
    {
        foreach (var type in schemaTypes)
        {
            if (SchemaTypeMap.TryGetValue(type, out var category))
                scores[category] = scores.GetValueOrDefault(category) + 30;
        }
    }

    private static void ApplyOgTypeSignal(string ogType, Dictionary<string, int> scores)
    {
        switch (ogType)
        {
            case "product":
                scores["Thương mại điện tử"] = scores.GetValueOrDefault("Thương mại điện tử") + 25;
                break;
            case "article":
                scores["Tin tức"] = scores.GetValueOrDefault("Tin tức") + 20;
                break;
            case "website":
                break;
        }
    }

    private static void ApplyEduTldBoost(string host, Dictionary<string, int> scores)
    {
        if (host.EndsWith(".edu", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".edu.vn", StringComparison.OrdinalIgnoreCase) ||
            host.Contains(".hutech.") || host.Contains(".hcmus.") || host.EndsWith(".ac.vn", StringComparison.OrdinalIgnoreCase))
        {
            scores["Giáo dục / Khóa học"] = scores.GetValueOrDefault("Giáo dục / Khóa học") + 25;
        }
    }

    private static void ApplyConflictResolution(PageContext ctx, Dictionary<string, int> scores)
    {
        var game = scores.GetValueOrDefault("Game");
        var edu = scores.GetValueOrDefault("Giáo dục / Khóa học");
        var tech = scores.GetValueOrDefault("Công nghệ");
        var ecommerce = scores.GetValueOrDefault("Thương mại điện tử");
        var strongEcommerce = HasStrongEcommerceSignal(ctx);

        if (game >= 8 && ecommerce > 0 && !strongEcommerce)
            scores["Thương mại điện tử"] = Math.Max(0, ecommerce - (int)(game * 0.85));

        if (edu >= 10 && tech > 0)
            scores["Công nghệ"] = Math.Max(0, tech - edu / 2);

        var news = scores.GetValueOrDefault("Tin tức");
        if (news >= 12)
            scores["Giải trí"] = Math.Max(0, scores.GetValueOrDefault("Giải trí") - news / 3);

        if (ecommerce > 0 && !strongEcommerce && ecommerce < 20)
            scores["Thương mại điện tử"] = Math.Max(0, ecommerce - 8);
    }

    private static bool HasStrongEcommerceSignal(PageContext ctx)
    {
        if (ctx.OgType == "product") return true;
        if (ctx.SchemaTypes.Any(t => t.Equals("Product", StringComparison.OrdinalIgnoreCase))) return true;
        if (Regex.IsMatch(ctx.FullUrl, @"shopee|lazada|tiki\.vn|amazon\.|ebay\.|/cart|/checkout", RegexOptions.IgnoreCase))
            return true;

        var head = $"{ctx.Title} {ctx.H1} {ctx.MetaDesc} {ctx.OgTitle}";
        ReadOnlySpan<string> strongPhrases = ["add to cart", "giỏ hàng", "gio hang", "checkout", "đặt hàng", "dat hang", "mua ngay"];
        foreach (var phrase in strongPhrases)
        {
            if (head.Contains(phrase, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static CategoryResult PickResult(Dictionary<string, int> scores)
    {
        if (scores.Count == 0)
            return new CategoryResult { PrimaryCategory = "Tổng quát", Tags = ["Tổng quát"] };

        var ranked = scores.OrderByDescending(x => x.Value).ToList();
        var top = ranked[0];
        var second = ranked.Count > 1 ? ranked[1].Value : 0;

        if (top.Value < MinPrimaryScore || top.Value - second < MinMargin)
            return new CategoryResult { PrimaryCategory = "Tổng quát", Tags = BuildTags(ranked, "Tổng quát") };

        return BuildResult(top.Key, scores);
    }

    private static CategoryResult BuildResult(string primary, Dictionary<string, int> scores)
    {
        var ranked = scores.OrderByDescending(x => x.Value).ToList();
        return new CategoryResult
        {
            PrimaryCategory = primary,
            Tags = BuildTags(ranked, primary)
        };
    }

    private static List<string> BuildTags(List<KeyValuePair<string, int>> ranked, string primary)
    {
        if (ranked.Count == 0) return [primary];

        var max = ranked[0].Value;
        var threshold = Math.Max(MinPrimaryScore, max * 0.45);

        var tags = ranked
            .Where(x => x.Value >= threshold)
            .Take(3)
            .Select(x => x.Key)
            .ToList();

        if (!tags.Contains(primary))
            tags.Insert(0, primary);

        return tags;
    }

    private static bool TryDomainOverride(string host, out string category)
    {
        if (DomainOverrides.TryGetValue(host, out category!))
            return true;

        foreach (var (domain, cat) in DomainOverrides)
        {
            if (host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase))
            {
                category = cat;
                return true;
            }
        }

        category = "";
        return false;
    }

    private static List<string> ExtractSchemaTypes(HtmlDocument doc)
    {
        var types = new List<string>();
        var scripts = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
        if (scripts == null) return types;

        foreach (var script in scripts)
        {
            try
            {
                using var doc_json = JsonDocument.Parse(script.InnerText);
                CollectSchemaTypes(doc_json.RootElement, types);
            }
            catch
            {
                // JSON-LD không hợp lệ — bỏ qua
            }
        }

        return types.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void CollectSchemaTypes(JsonElement element, List<string> types)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("@type", out var typeProp))
                AddTypeValue(typeProp, types);

            foreach (var prop in element.EnumerateObject())
            {
                if (prop.Name is "@graph" or "itemListElement" or "mainEntity")
                    CollectSchemaTypes(prop.Value, types);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                CollectSchemaTypes(item, types);
        }
    }

    private static void AddTypeValue(JsonElement typeProp, List<string> types)
    {
        if (typeProp.ValueKind == JsonValueKind.String)
            types.Add(typeProp.GetString() ?? "");
        else if (typeProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in typeProp.EnumerateArray())
                if (t.ValueKind == JsonValueKind.String)
                    types.Add(t.GetString() ?? "");
        }
    }

    private static string GetMetaContent(HtmlDocument doc, string name)
    {
        var node = doc.DocumentNode.SelectSingleNode(
            $"//meta[translate(@name,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz')='{name}']");
        return node?.GetAttributeValue("content", "") ?? "";
    }

    private static string GetMetaProperty(HtmlDocument doc, string property)
    {
        var node = doc.DocumentNode.SelectSingleNode(
            $"//meta[translate(@property,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz')='{property}']");
        return node?.GetAttributeValue("content", "") ?? "";
    }

    private static string CleanText(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var text = HtmlEntity.DeEntitize(raw);
        return Regex.Replace(text.ToLowerInvariant(), @"\s+", " ").Trim();
    }

    private static int CountWeighted(string text, string phrase, int weight, bool wordBoundary)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(phrase)) return 0;
        var count = wordBoundary && phrase.All(c => c < 128)
            ? Regex.Matches(text, $@"\b{Regex.Escape(phrase)}\b", RegexOptions.IgnoreCase).Count
            : CountPhrase(text, phrase);
        return count * weight;
    }

    private static int CountPhrase(string text, string phrase)
    {
        int count = 0, index = 0;
        while ((index = text.IndexOf(phrase, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += phrase.Length;
        }
        return count;
    }

    private sealed class PageContext
    {
        public string Host { get; init; } = "";
        public string Path { get; init; } = "";
        public string FullUrl { get; init; } = "";
        public string Title { get; init; } = "";
        public string MetaDesc { get; init; } = "";
        public string OgTitle { get; init; } = "";
        public string OgDesc { get; init; } = "";
        public string OgType { get; init; } = "";
        public string H1 { get; init; } = "";
        public string Body { get; init; } = "";
        public List<string> SchemaTypes { get; init; } = new();
    }
}
