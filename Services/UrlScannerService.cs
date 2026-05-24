using System.Diagnostics;
using System.Net;
using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Models;

namespace NetURLScanner.Services
{
    public class UrlScannerService
    {
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

            var risk = AnalyzeRisk(url, result, trustedBrands);

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
            Dictionary<string, string> trustedBrands)
        {
            int score = 0;
            List<string> reasons = new();

            Uri? uri = null;

            try
            {
                uri = new Uri(url);
            }
            catch
            {
                score += 60;
                reasons.Add("URL không đúng định dạng");
            }

            string lowerUrl = url.ToLower();
            string host = uri?.Host.ToLower() ?? lowerUrl;
            string path = uri?.AbsolutePath.ToLower() ?? "";
            string query = uri?.Query.ToLower() ?? "";

            var blacklistResult = CheckBlacklist(host).Result;

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

            if (!scan.IsHttps)
            {
                score += 15;
                reasons.Add("URL không sử dụng HTTPS");
            }

            if (scan.Status == "Offline")
            {
                score += 35;
                reasons.Add("URL không phản hồi hoặc bị lỗi kết nối, cần thận trọng khi truy cập");
            }

            if (scan.Status == "Redirect")
            {
                score += 10;
                reasons.Add("URL có chuyển hướng");
            }

            if (scan.Status == "Server Error")
            {
                score += 15;
                reasons.Add("Máy chủ trả về lỗi 5xx");
            }

            if (scan.ResponseTimeMs > 3000)
            {
                score += 10;
                reasons.Add("Thời gian phản hồi chậm hơn 3 giây");
            }

            if (scan.ResponseTimeMs > 7000)
            {
                score += 15;
                reasons.Add("Thời gian phản hồi rất chậm");
            }

            if (url.Length > 100)
            {
                score += 10;
                reasons.Add("URL quá dài");
            }

            if (query.Length > 80)
            {
                score += 10;
                reasons.Add("Query string dài bất thường");
            }

            if (host.Count(c => c == '-') >= 2)
            {
                score += 10;
                reasons.Add("Domain có nhiều dấu gạch ngang");
            }

            if (IPAddress.TryParse(host, out _))
            {
                score += 20;
                reasons.Add("URL sử dụng địa chỉ IP thay vì domain");
            }

            if (uri != null && !uri.IsDefaultPort)
            {
                score += 10;
                reasons.Add($"URL sử dụng port không phổ biến: {uri.Port}");
            }

            int subdomainCount = CountSubdomains(host);

            if (subdomainCount >= 3)
            {
                score += 15;
                reasons.Add("Domain có quá nhiều subdomain");
            }

            if (HasNonAsciiCharacter(host))
            {
                score += 30;
                reasons.Add("Domain chứa ký tự Unicode bất thường, có thể là homograph attack");
            }

            if (host.Contains("xn--"))
            {
                score += 25;
                reasons.Add("Domain sử dụng punycode, có thể giả mạo ký tự");
            }

            string[] suspiciousWords =
            {
                "login", "verify", "account", "wallet",
                "gift", "free", "bonus", "secure", "update",
                "confirm", "password", "payment", "admin",
                "signin", "reset", "otp", "token", "invoice",
                "billing", "crypto", "airdrop"
            };

            if (!isOfficialTrustedDomain)
            {
                foreach (var word in suspiciousWords)
                {
                    if (lowerUrl.Contains(word))
                    {
                        score += 8;
                        reasons.Add($"URL chứa từ khóa đáng ngờ: {word}");
                    }
                }
            }

            string[] gamblingWords =
            {
                "bet", "betting", "casino", "gambling", "poker",
                "slot", "jackpot", "odds", "wager", "baccarat",
                "roulette", "sportsbook", "bookmaker",

                "taixiu", "tai-xiu", "xocdia", "xoc-dia",
                "keonhacai", "keo-nha-cai", "nhacai", "nha-cai",
                "cacuoc", "ca-cuoc", "danhbai", "danh-bai",
                "bong88", "f8bet", "fun88", "m88", "kubet",
                "fb88", "w88", "188bet"
            };

            bool hasGamblingKeyword = false;

            foreach (var word in gamblingWords)
            {
                if (lowerUrl.Contains(word))
                {
                    hasGamblingKeyword = true;

                    score += 60;

                    reasons.Add($"URL chứa từ khóa liên quan đến cá cược/cờ bạc: {word}");
                    reasons.Add("URL thuộc nhóm nội dung rủi ro cao, có thể tiềm ẩn nguy cơ mất tài sản, lừa đảo tài chính hoặc thu thập thông tin cá nhân.");

                    break;
                }
            }

            string[] financialActionWords =
            {
                "login", "account", "payment", "deposit", "withdraw",
                "wallet", "nap-tien", "ruttien", "rut-tien",
                "dangnhap", "dang-nhap", "register", "signup",
                "cash", "money", "banking"
            };

            bool hasFinancialActionWord = financialActionWords.Any(word =>
                lowerUrl.Contains(word));

            if (hasGamblingKeyword && hasFinancialActionWord)
            {
                score += 20;
                reasons.Add("URL cá cược/cờ bạc có liên quan đến đăng nhập, tài khoản, nạp tiền, rút tiền hoặc giao dịch tài chính.");
            }

            string[] dangerousPathWords =
            {
                "wp-admin", "admin", "phpmyadmin", "cpanel", "shell", "cmd"
            };

            if (!isOfficialTrustedDomain)
            {
                foreach (var word in dangerousPathWords)
                {
                    if (path.Contains(word))
                    {
                        score += 12;
                        reasons.Add($"Đường dẫn chứa từ khóa nhạy cảm: {word}");
                    }
                }
            }

            var brandRisk = CheckBrandImpersonation(host, url, trustedBrands);
            score += brandRisk.Score;
            reasons.AddRange(brandRisk.Reasons);

            var tldRisk = CheckTldRisk(host);
            score += tldRisk.Score;
            reasons.AddRange(tldRisk.Reasons);

            score = Math.Min(score, 100);

            string level = score switch
            {
                <= 30 => "Safe",
                <= 60 => "Warning",
                _ => "Suspicious"
            };

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

        private RiskResult CheckBrandImpersonation(
            string host,
            string fullUrl,
            Dictionary<string, string> trustedBrands)
        {
            int score = 0;
            List<string> reasons = new();

            string mainName = GetMainDomainName(host);
            string normalizedMainName = NormalizeLookalike(mainName);
            string lowerFullUrl = fullUrl.ToLower();

            bool isOfficialTrustedDomain = trustedBrands.Values.Any(domain =>
                host == domain || host.EndsWith("." + domain));

            if (isOfficialTrustedDomain)
            {
                return new RiskResult
                {
                    Score = 0,
                    Level = "",
                    Reasons = new List<string>()
                };
            }

            foreach (var brand in trustedBrands)
            {
                string brandName = brand.Key.ToLower();
                string officialDomain = brand.Value.ToLower();

                if (host == officialDomain || host.EndsWith("." + officialDomain))
                {
                    continue;
                }

                if (lowerFullUrl.Contains(brandName) && !host.Contains(brandName))
                {
                    score += 35;
                    reasons.Add($"URL chứa tên thương hiệu {brandName} trong đường dẫn hoặc tham số nhưng domain thật không phải domain chính thức");
                }

                if (host.Contains(officialDomain))
                {
                    score += 35;
                    reasons.Add($"Domain chứa {officialDomain} nhưng không phải domain chính thức");
                }

                bool isShortBrand = brandName.Length <= 3;

                if (!isShortBrand && mainName.Contains(brandName) && host != officialDomain)
                {
                    score += 25;
                    reasons.Add($"Domain có chứa tên thương hiệu {brandName} nhưng không phải domain chính thức");
                }

                if (isShortBrand && mainName == brandName && host != officialDomain)
                {
                    score += 25;
                    reasons.Add($"Domain sử dụng tên viết tắt {brandName} nhưng không phải domain chính thức");
                }

                if (normalizedMainName == brandName && mainName != brandName)
                {
                    score += 30;
                    reasons.Add($"Domain dùng ký tự thay thế để giống {brandName}");
                }

                bool canUseDistanceCheck =
                    mainName.Length >= 5 &&
                    brandName.Length >= 5 &&
                    Math.Abs(mainName.Length - brandName.Length) <= 2;

                int distance = LevenshteinDistance(mainName, brandName);

                if (canUseDistanceCheck && distance > 0 && distance <= 2)
                {
                    score += 25;
                    reasons.Add($"Domain gần giống thương hiệu {brandName}");
                }
            }

            return new RiskResult
            {
                Score = score,
                Level = "",
                Reasons = reasons.Distinct().ToList()
            };
        }

        private RiskResult CheckTldRisk(string host)
        {
            int score = 0;
            List<string> reasons = new();

            string[] riskyTlds =
            {
                ".xyz", ".top", ".click", ".info", ".shop",
                ".online", ".work", ".rest", ".site", ".live",
                ".vip", ".club", ".buzz", ".icu", ".cyou", ".monster"
            };

            if (riskyTlds.Any(tld => host.EndsWith(tld)))
            {
                score += 10;
                reasons.Add("Domain sử dụng đuôi tên miền có mức rủi ro cao");
            }

            return new RiskResult
            {
                Score = score,
                Level = "",
                Reasons = reasons
            };
        }

        private int CountSubdomains(string host)
        {
            var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length <= 2)
                return 0;

            return parts.Length - 2;
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

            if (parts.Length < 2)
                return host;

            return parts[^2];
        }

        private bool HasNonAsciiCharacter(string text)
        {
            return text.Any(c => c > 127);
        }

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
                        Math.Min(
                            dp[i - 1, j] + 1,
                            dp[i, j - 1] + 1
                        ),
                        dp[i - 1, j - 1] + cost
                    );
                }
            }

            return dp[a.Length, b.Length];
        }
    }
}