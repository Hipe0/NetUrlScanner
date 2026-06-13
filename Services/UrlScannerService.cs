using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Models;

namespace NetURLScanner.Services
{
    public class UrlScannerService
    {
        private static readonly HashSet<int> CommonPorts = new() { 80, 443, 8080, 8443 };

        private readonly ApplicationDbContext _context;

        public UrlScannerService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<UrlScan> ScanAsync(string inputUrl)
        {
            var url = NormalizeUrl(inputUrl);

            var result = new UrlScan
            {
                Url = url,
                ScannedAt = DateTime.Now,
                IsHttps = url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            };

            Uri? uri = null;
            try
            {
                uri = new Uri(url);
                var geo = await GetGeolocationAsync(uri.Host);
                result.IpAddress = geo.Ip;
                result.CountryName = geo.Country;
                result.CountryCode = geo.CountryCode;
                result.City = geo.City;
                result.Isp = geo.Isp;
                result.Latitude = geo.Lat;
                result.Longitude = geo.Lon;
            }
            catch
            {
                result.IpAddress = "-";
                result.CountryName = "Unknown";
                result.CountryCode = "-";
                result.City = "Unknown";
                result.Isp = "Unknown";
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var handler = new HttpClientHandler
                {
                    AllowAutoRedirect = false
                };

                using var httpClient = new HttpClient(handler)
                {
                    Timeout = TimeSpan.FromSeconds(10)
                };

                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NetURLScanner/1.0");

                var response = await httpClient.GetAsync(url);

                stopwatch.Stop();

                result.StatusCode = (int)response.StatusCode;
                result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;

                if (response.IsSuccessStatusCode)
                {
                    result.Status = "Online";
                }
                else if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400)
                {
                    result.Status = "Redirect";
                }
                else if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                {
                    result.Status = "Client Error";
                }
                else if ((int)response.StatusCode >= 500)
                {
                    result.Status = "Server Error";
                }
                else
                {
                    result.Status = "Warning";
                }
            }
            catch (TaskCanceledException)
            {
                stopwatch.Stop();

                result.Status = "Offline";
                result.ErrorMessage = "Timeout - URL phản hồi quá lâu";
                result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            }
            catch (HttpRequestException ex)
            {
                stopwatch.Stop();

                result.Status = "Offline";
                result.ErrorMessage = ex.Message;
                result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                result.Status = "Offline";
                result.ErrorMessage = ex.Message;
                result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            }

            var trustedBrands = await GetTrustedBrandsAsync();
            var blacklistResult = await CheckBlacklist(uri?.Host ?? string.Empty);
            var risk = AnalyzeRisk(url, result, trustedBrands, blacklistResult);

            result.RiskScore = risk.Score;
            result.RiskLevel = risk.Level;
            result.Reasons = string.Join("; ", risk.Reasons);

            return result;
        }

        private async Task<Dictionary<string, string>> GetTrustedBrandsAsync()
        {
            var trustedBrands = TrustedBrandDefaults.GetDefaultBrands();

            var databaseBrands = await _context.TrustedBrands
                .Where(x => x.IsActive)
                .ToListAsync();

            foreach (var brand in databaseBrands)
            {
                string brandName = brand.BrandName.Trim().ToLower();
                string domain = brand.OfficialDomain.Trim().ToLower();

                if (string.IsNullOrWhiteSpace(brandName) || string.IsNullOrWhiteSpace(domain))
                {
                    continue;
                }

                if (!trustedBrands.ContainsKey(brandName))
                {
                    trustedBrands.Add(brandName, domain);
                }
                else
                {
                    trustedBrands[brandName] = domain;
                }
            }

            return trustedBrands;
        }

        private async Task<(bool IsBlacklisted, string Category, string Severity, string Reason)> CheckBlacklist(string host)
        {
            var blacklistedDomains = await _context.BlacklistedDomains
                .Where(x => x.IsActive)
                .ToListAsync();

            foreach (var item in blacklistedDomains)
            {
                string domain = item.Domain.Trim().ToLower();

                if (host == domain || host.EndsWith("." + domain))
                {
                    return (true, item.Category, item.Severity, item.Reason);
                }
            }

            return (false, "", "", "");
        }

        private string NormalizeUrl(string url)
        {
            url = url.Trim();

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            return url;
        }

        private RiskResult AnalyzeRisk(
            string url,
            UrlScan scan,
            Dictionary<string, string> trustedBrands,
            (bool IsBlacklisted, string Category, string Severity, string Reason) blacklistResult)
        {
            var reasons = new List<string>();
            var categoryScores = new Dictionary<string, int>();

            Uri? uri;
            try
            {
                uri = new Uri(url);
            }
            catch
            {
                return new RiskResult
                {
                    Score = 60,
                    Level = "Suspicious",
                    Reasons = new List<string> { "URL không đúng định dạng" }
                };
            }

            string lowerUrl = url.ToLower();
            string host = uri.Host.ToLower();
            string path = uri.AbsolutePath.ToLower();
            string query = uri.Query.ToLower();

            if (blacklistResult.IsBlacklisted)
            {
                return new RiskResult
                {
                    Score = 100,
                    Level = "Suspicious",
                    Reasons = new List<string>
                    {
                        $"Domain {host} nằm trong blacklist của hệ thống.",
                        $"Loại rủi ro: {blacklistResult.Category}.",
                        $"Mức độ: {blacklistResult.Severity}.",
                        $"Lý do: {blacklistResult.Reason}"
                    }
                };
            }

            bool isOfficialTrustedDomain = trustedBrands.Values.Any(domain =>
                host == domain || host.EndsWith("." + domain));

            // Nhóm kết nối & phản hồi (cap 45)
            if (!scan.IsHttps)
            {
                AddCategoryScore(categoryScores, reasons, "connection", 12, "URL không sử dụng HTTPS", 45);
            }

            if (scan.Status == "Offline")
            {
                AddCategoryScore(categoryScores, reasons, "connection", 30,
                    "URL không phản hồi hoặc bị lỗi kết nối, cần thận trọng khi truy cập", 45);
            }
            else if (scan.Status == "Redirect")
            {
                AddCategoryScore(categoryScores, reasons, "connection", 8, "URL có chuyển hướng", 45);
            }
            else if (scan.Status == "Server Error")
            {
                AddCategoryScore(categoryScores, reasons, "connection", 12, "Máy chủ trả về lỗi 5xx", 45);
            }

            // Hiệu năng: chỉ lấy mức cao nhất (cap 15)
            if (scan.ResponseTimeMs > 7000)
            {
                AddCategoryScore(categoryScores, reasons, "performance", 15,
                    "Thời gian phản hồi rất chậm (> 7 giây)", 15);
            }
            else if (scan.ResponseTimeMs > 3000)
            {
                AddCategoryScore(categoryScores, reasons, "performance", 8,
                    "Thời gian phản hồi chậm (> 3 giây)", 15);
            }

            // Cấu trúc URL (cap 35)
            if (url.Length > 100)
            {
                AddCategoryScore(categoryScores, reasons, "structure", 8, "URL quá dài", 35);
            }

            if (query.Length > 80)
            {
                AddCategoryScore(categoryScores, reasons, "structure", 8, "Query string dài bất thường", 35);
            }

            if (host.Count(c => c == '-') >= 3)
            {
                AddCategoryScore(categoryScores, reasons, "structure", 10,
                    "Domain có nhiều dấu gạch ngang", 35);
            }

            if (IPAddress.TryParse(host, out _))
            {
                AddCategoryScore(categoryScores, reasons, "structure", 18,
                    "URL sử dụng địa chỉ IP thay vì domain", 35);
            }

            if (!uri.IsDefaultPort && !CommonPorts.Contains(uri.Port))
            {
                AddCategoryScore(categoryScores, reasons, "structure", 8,
                    $"URL sử dụng port không phổ biến: {uri.Port}", 35);
            }

            if (CountSubdomains(host) >= 3)
            {
                AddCategoryScore(categoryScores, reasons, "structure", 12,
                    "Domain có quá nhiều subdomain", 35);
            }

            // Homograph / Punycode (cap 30)
            if (HasNonAsciiCharacter(host))
            {
                AddCategoryScore(categoryScores, reasons, "spoofing", 28,
                    "Domain chứa ký tự Unicode bất thường, có thể là homograph attack", 30);
            }
            else if (host.Contains("xn--"))
            {
                AddCategoryScore(categoryScores, reasons, "spoofing", 22,
                    "Domain sử dụng punycode, có thể giả mạo ký tự", 30);
            }

            string[] gamblingWords =
            {
                "bet", "betting", "casino", "gambling", "poker", "slot", "jackpot",
                "odds", "wager", "baccarat", "roulette", "sportsbook", "bookmaker",
                "taixiu", "tai-xiu", "xocdia", "xoc-dia", "keonhacai", "keo-nha-cai",
                "nhacai", "nha-cai", "cacuoc", "ca-cuoc", "danhbai", "danh-bai",
                "bong88", "f8bet", "fun88", "m88", "kubet", "fb88", "w88", "188bet"
            };

            string[] financialActionWords =
            {
                "login", "account", "payment", "deposit", "withdraw", "wallet",
                "nap-tien", "ruttien", "rut-tien", "dangnhap", "dang-nhap",
                "register", "signup", "cash", "money", "banking"
            };

            string? gamblingMatch = gamblingWords.FirstOrDefault(word => lowerUrl.Contains(word));
            bool hasGamblingKeyword = gamblingMatch != null;

            if (hasGamblingKeyword)
            {
                categoryScores["gambling"] = 55;
                reasons.Add($"URL chứa từ khóa liên quan đến cá cược/cờ bạc: {gamblingMatch}");
                reasons.Add("URL thuộc nhóm nội dung rủi ro cao, có thể tiềm ẩn nguy cơ mất tài sản, lừa đảo tài chính hoặc thu thập thông tin cá nhân.");

                if (financialActionWords.Any(word => lowerUrl.Contains(word)))
                {
                    categoryScores["gambling"] = 70;
                    reasons.Add("URL cá cược/cờ bạc có liên quan đến đăng nhập, tài khoản, nạp tiền, rút tiền hoặc giao dịch tài chính.");
                }
            }

            if (!isOfficialTrustedDomain)
            {
                string[] suspiciousWords =
                {
                    "login", "verify", "account", "wallet", "gift", "free", "bonus",
                    "secure", "update", "confirm", "password", "payment", "admin",
                    "signin", "reset", "otp", "token", "invoice", "billing", "crypto", "airdrop"
                };

                // Tối đa 3 từ khóa, cap 24 — match theo ranh giới từ
                int suspiciousCount = 0;
                foreach (var word in suspiciousWords)
                {
                    if (!ContainsKeyword(lowerUrl, word))
                    {
                        continue;
                    }

                    AddCategoryScore(categoryScores, reasons, "keywords", 8,
                        $"URL chứa từ khóa đáng ngờ: {word}", 24);

                    suspiciousCount++;
                    if (suspiciousCount >= 3)
                    {
                        break;
                    }
                }

                string[] dangerousPathWords =
                {
                    "wp-admin", "phpmyadmin", "cpanel", "shell", "cmd"
                };

                foreach (var word in dangerousPathWords)
                {
                    if (ContainsPathSegment(path, word))
                    {
                        AddCategoryScore(categoryScores, reasons, "path", 12,
                            $"Đường dẫn chứa từ khóa nhạy cảm: {word}", 24);
                    }
                }
            }

            // Brand impersonation: chỉ lấy match mạnh nhất (cap 40)
            var brandRisk = CheckBrandImpersonation(host, url, trustedBrands);
            if (brandRisk.Score > 0)
            {
                categoryScores["brand"] = Math.Min(brandRisk.Score, 40);
                reasons.AddRange(brandRisk.Reasons);
            }

            // TLD rủi ro: cộng mạnh hơn khi đã có tín hiệu khác
            var tldRisk = CheckTldRisk(host, categoryScores);
            if (tldRisk.Score > 0)
            {
                categoryScores["tld"] = tldRisk.Score;
                reasons.AddRange(tldRisk.Reasons);
            }

            int score = Math.Min(categoryScores.Values.Sum(), 100);
            string level = DetermineRiskLevel(score, categoryScores, hasGamblingKeyword);

            if (!reasons.Any())
            {
                reasons.Add("Không phát hiện dấu hiệu bất thường");
            }

            return new RiskResult
            {
                Score = score,
                Level = level,
                Reasons = reasons.Distinct().ToList()
            };
        }

        private static void AddCategoryScore(
            Dictionary<string, int> categories,
            List<string> reasons,
            string category,
            int points,
            string reason,
            int cap)
        {
            categories.TryGetValue(category, out int current);
            if (current >= cap)
            {
                return;
            }

            int added = Math.Min(points, cap - current);
            categories[category] = current + added;
            reasons.Add(reason);
        }

        private static string DetermineRiskLevel(int score, Dictionary<string, int> categories, bool hasGambling)
        {
            if (hasGambling || categories.GetValueOrDefault("brand") >= 35)
            {
                return "Suspicious";
            }

            if (categories.GetValueOrDefault("spoofing") >= 22 || categories.GetValueOrDefault("brand") >= 25)
            {
                return score <= 45 ? "Warning" : "Suspicious";
            }

            if (score <= 25) return "Safe";
            if (score <= 55) return "Warning";
            return "Suspicious";
        }

        private RiskResult CheckBrandImpersonation(
            string host,
            string fullUrl,
            Dictionary<string, string> trustedBrands)
        {
            bool isOfficialTrustedDomain = trustedBrands.Values.Any(domain =>
                host == domain || host.EndsWith("." + domain));

            if (isOfficialTrustedDomain)
            {
                return new RiskResult { Score = 0, Level = "", Reasons = new List<string>() };
            }

            string mainName = GetMainDomainName(host);
            string normalizedMainName = NormalizeLookalike(mainName);
            string lowerFullUrl = fullUrl.ToLower();

            int bestScore = 0;
            var bestReasons = new List<string>();

            foreach (var brand in trustedBrands)
            {
                string brandName = brand.Key.ToLower();
                string officialDomain = brand.Value.ToLower();

                if (host == officialDomain || host.EndsWith("." + officialDomain))
                {
                    continue;
                }

                int matchScore = 0;
                var matchReasons = new List<string>();

                if (normalizedMainName == brandName && mainName != brandName)
                {
                    matchScore = Math.Max(matchScore, 32);
                    matchReasons.Add($"Domain dùng ký tự thay thế để giống {brandName}");
                }

                if (lowerFullUrl.Contains(brandName) && !host.Contains(brandName))
                {
                    matchScore = Math.Max(matchScore, 35);
                    matchReasons.Add($"URL chứa tên thương hiệu {brandName} trong đường dẫn hoặc tham số nhưng domain thật không phải domain chính thức");
                }

                if (host.Contains(officialDomain))
                {
                    matchScore = Math.Max(matchScore, 35);
                    matchReasons.Add($"Domain chứa {officialDomain} nhưng không phải domain chính thức");
                }

                bool isShortBrand = brandName.Length <= 3;

                if (!isShortBrand && mainName.Contains(brandName) && host != officialDomain)
                {
                    matchScore = Math.Max(matchScore, 25);
                    matchReasons.Add($"Domain có chứa tên thương hiệu {brandName} nhưng không phải domain chính thức");
                }

                if (isShortBrand && mainName == brandName && host != officialDomain)
                {
                    matchScore = Math.Max(matchScore, 25);
                    matchReasons.Add($"Domain sử dụng tên viết tắt {brandName} nhưng không phải domain chính thức");
                }

                bool canUseDistanceCheck =
                    mainName.Length >= 5 &&
                    brandName.Length >= 5 &&
                    Math.Abs(mainName.Length - brandName.Length) <= 2;

                if (canUseDistanceCheck)
                {
                    int distance = LevenshteinDistance(mainName, brandName);
                    if (distance > 0 && distance <= 2)
                    {
                        matchScore = Math.Max(matchScore, 28);
                        matchReasons.Add($"Domain gần giống thương hiệu {brandName}");
                    }
                }

                if (matchScore > bestScore)
                {
                    bestScore = matchScore;
                    bestReasons = matchReasons;
                }
            }

            return new RiskResult
            {
                Score = bestScore,
                Level = "",
                Reasons = bestReasons.Distinct().ToList()
            };
        }

        private RiskResult CheckTldRisk(string host, Dictionary<string, int> categoryScores)
        {
            string[] riskyTlds =
            {
                ".xyz", ".top", ".click", ".info", ".shop", ".online", ".work",
                ".rest", ".site", ".live", ".vip", ".club", ".buzz", ".icu", ".cyou", ".monster"
            };

            if (!riskyTlds.Any(tld => host.EndsWith(tld)))
            {
                return new RiskResult { Score = 0, Level = "", Reasons = new List<string>() };
            }

            int otherSignals = categoryScores.Where(x => x.Key != "tld").Sum(x => x.Value);
            int score = otherSignals >= 15 ? 12 : 5;

            return new RiskResult
            {
                Score = score,
                Level = "",
                Reasons = new List<string>
                {
                    otherSignals >= 15
                        ? "Domain dùng TLD rủi ro kết hợp với các dấu hiệu bất thường khác"
                        : "Domain sử dụng đuôi tên miền có mức rủi ro cao"
                }
            };
        }

        private static bool ContainsKeyword(string text, string keyword)
        {
            string pattern = $@"(^|[/\?&=\-_.]){Regex.Escape(keyword)}([/\?&=\-_.]|$)";
            return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase);
        }

        private static bool ContainsPathSegment(string path, string segment)
        {
            return path.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Any(part => part.Equals(segment, StringComparison.OrdinalIgnoreCase)
                          || part.StartsWith(segment + "-", StringComparison.OrdinalIgnoreCase)
                          || part.EndsWith("-" + segment, StringComparison.OrdinalIgnoreCase));
        }

        private int CountSubdomains(string host)
        {
            var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length <= 2 ? 0 : parts.Length - 2;
        }

        private string GetMainDomainName(string host)
        {
            host = host.ToLower().Trim();

            string[] vietnameseSecondLevelTlds =
            {
                ".com.vn", ".net.vn", ".org.vn", ".gov.vn", ".edu.vn",
                ".biz.vn", ".info.vn", ".name.vn", ".pro.vn", ".health.vn"
            };

            foreach (var tld in vietnameseSecondLevelTlds)
            {
                if (host.EndsWith(tld))
                {
                    string withoutTld = host.Substring(0, host.Length - tld.Length);
                    var labels = withoutTld.Split('.', StringSplitOptions.RemoveEmptyEntries);

                    if (labels.Length > 0)
                    {
                        return labels[^1];
                    }
                }
            }

            var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length < 2 ? host : parts[^2];
        }

        private bool HasNonAsciiCharacter(string text) => text.Any(c => c > 127);

        private string NormalizeLookalike(string input)
        {
            return input.ToLower()
                .Replace("0", "o")
                .Replace("1", "l")
                .Replace("3", "e")
                .Replace("5", "s")
                .Replace("@", "a")
                .Replace("$", "s");
        }

        private int LevenshteinDistance(string a, string b)
        {
            int[,] dp = new int[a.Length + 1, b.Length + 1];

            for (int i = 0; i <= a.Length; i++)
                dp[i, 0] = i;

            for (int j = 0; j <= b.Length; j++)
                dp[0, j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;

                    dp[i, j] = Math.Min(
                        Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                        dp[i - 1, j - 1] + cost);
                }
            }

            return dp[a.Length, b.Length];
        }

        private async Task<(string Ip, string Country, string CountryCode, string City, string Isp, double? Lat, double? Lon)> GetGeolocationAsync(string host)
        {
            try
            {
                var addresses = await Dns.GetHostAddressesAsync(host);
                var ipv4 = addresses.FirstOrDefault(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.ToString();

                if (string.IsNullOrEmpty(ipv4))
                {
                    return ("-", "Unknown", "-", "Unknown", "Unknown", null, null);
                }

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                var response = await client.GetAsync($"http://ip-api.com/json/{ipv4}");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadFromJsonAsync<IpApiResponse>();
                    if (json != null && json.status == "success")
                    {
                        return (ipv4, json.country, json.countryCode, json.city, json.isp, json.lat, json.lon);
                    }
                }

                return (ipv4, "Unknown", "-", "Unknown", "Unknown", null, null);
            }
            catch
            {
                return ("-", "Unknown", "-", "Unknown", "Unknown", null, null);
            }
        }
    }

    public class IpApiResponse
    {
        public string status { get; set; } = string.Empty;
        public string country { get; set; } = string.Empty;
        public string countryCode { get; set; } = string.Empty;
        public string city { get; set; } = string.Empty;
        public string isp { get; set; } = string.Empty;
        public double lat { get; set; }
        public double lon { get; set; }
    }
}
