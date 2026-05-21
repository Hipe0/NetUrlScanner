using System.Diagnostics;
using System.Net;
using NetURLScanner.Models;

namespace NetURLScanner.Services
{
    public class UrlScannerService
    {
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

            var risk = AnalyzeRisk(url, result);

            result.RiskScore = risk.Score;
            result.RiskLevel = risk.Level;
            result.Reasons = string.Join("; ", risk.Reasons);

            return result;
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

        private RiskResult AnalyzeRisk(string url, UrlScan scan)
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

            string host = uri?.Host.ToLower() ?? url.ToLower();
            string path = uri?.AbsolutePath.ToLower() ?? "";
            string query = uri?.Query.ToLower() ?? "";

            if (!scan.IsHttps)
            {
                score += 15;
                reasons.Add("URL không sử dụng HTTPS");
            }

            if (scan.Status == "Offline")
            {
                score += 20;
                reasons.Add("URL không phản hồi hoặc bị lỗi");
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
                "login", "verify", "account", "bank", "wallet",
                "gift", "free", "bonus", "secure", "update",
                "confirm", "password", "payment", "admin",
                "signin", "reset", "otp", "token", "invoice",
                "billing", "crypto", "airdrop"
            };

            foreach (var word in suspiciousWords)
            {
                if (url.ToLower().Contains(word))
                {
                    score += 8;
                    reasons.Add($"URL chứa từ khóa đáng ngờ: {word}");
                }
            }

            string[] dangerousPathWords =
            {
                "wp-admin", "admin", "phpmyadmin", "cpanel", "shell", "cmd"
            };

            foreach (var word in dangerousPathWords)
            {
                if (path.Contains(word))
                {
                    score += 12;
                    reasons.Add($"Đường dẫn chứa từ khóa nhạy cảm: {word}");
                }
            }

            var brandRisk = CheckBrandImpersonation(host);
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
                Reasons = reasons
            };
        }

        private RiskResult CheckBrandImpersonation(string host)
        {
            int score = 0;
            List<string> reasons = new();

            var trustedBrands = new Dictionary<string, string>
            {
                { "google", "google.com" },
                { "facebook", "facebook.com" },
                { "microsoft", "microsoft.com" },
                { "apple", "apple.com" },
                { "github", "github.com" },
                { "paypal", "paypal.com" },
                { "shopee", "shopee.vn" },
                { "lazada", "lazada.vn" },
                { "tiktok", "tiktok.com" },
                { "netflix", "netflix.com" },
                { "instagram", "instagram.com" },
                { "amazon", "amazon.com" },
                { "steam", "steampowered.com" }
            };

            string mainName = GetMainDomainName(host);
            string normalizedMainName = NormalizeLookalike(mainName);

            foreach (var brand in trustedBrands)
            {
                string brandName = brand.Key;
                string officialDomain = brand.Value;

                if (host == officialDomain || host.EndsWith("." + officialDomain))
                {
                    continue;
                }

                if (host.Contains(officialDomain))
                {
                    score += 35;
                    reasons.Add($"Domain chứa {officialDomain} nhưng không phải domain chính thức");
                }

                if (mainName.Contains(brandName) && host != officialDomain)
                {
                    score += 25;
                    reasons.Add($"Domain có chứa tên thương hiệu {brandName} nhưng không phải domain chính thức");
                }

                if (normalizedMainName == brandName && mainName != brandName)
                {
                    score += 30;
                    reasons.Add($"Domain dùng ký tự thay thế để giống {brandName}");
                }

                int distance = LevenshteinDistance(mainName, brandName);

                if (distance > 0 && distance <= 2)
                {
                    score += 25;
                    reasons.Add($"Domain gần giống thương hiệu {brandName}");
                }
            }

            return new RiskResult
            {
                Score = score,
                Level = "",
                Reasons = reasons
            };
        }

        private RiskResult CheckTldRisk(string host)
        {
            int score = 0;
            List<string> reasons = new();

            string[] riskyTlds =
            {
                ".xyz", ".top", ".click", ".info", ".shop",
                ".online", ".work", ".rest", ".site", ".live"
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