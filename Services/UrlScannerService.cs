using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Helpers;
using NetURLScanner.Models;

namespace NetURLScanner.Services
{
    /// <summary>
    /// Service lõi: quét URL, phân tích HTTP, geolocation, chấm điểm rủi ro rule-based.
    /// Được gọi từ UrlScannerController, BulkScan, API và OCR (sau khi trích URL).
    /// </summary>
    public class UrlScannerService
    {
        // Port web phổ biến — port lạ được cộng điểm nhóm structure.
        private static readonly HashSet<int> CommonPorts = new() { 80, 443, 8080, 8443 };

        // Từ khóa cá cược / cờ bạc — phát hiện → thường gán Suspicious.
        private static readonly string[] GamblingWords =
        {
            "bet", "betting", "casino", "gambling", "poker", "slot", "jackpot",
            "odds", "wager", "baccarat", "roulette", "sportsbook", "bookmaker",
            "taixiu", "tai-xiu", "xocdia", "xoc-dia", "keonhacai", "keo-nha-cai",
            "nhacai", "nha-cai", "cacuoc", "ca-cuoc", "danhbai", "danh-bai",
            "bong88", "f8bet", "fun88", "m88", "kubet", "fb88", "w88", "188bet"
        };

        // Danh sách từ khóa hành động tài chính nhạy cảm thường xuất hiện trong các trang lừa đảo hoặc cá cược.
        private static readonly string[] FinancialActionWords =
        {
            "login", "account", "payment", "deposit", "withdraw", "wallet",
            "nap-tien", "ruttien", "rut-tien", "dangnhap", "dang-nhap",
            "register", "signup", "cash", "money", "banking"
        };

        // Danh sách từ khóa đáng ngờ hay dùng để giả mạo trang đăng nhập, tài khoản ngân hàng, ví điện tử.
        private static readonly string[] SuspiciousWords =
        {
            "login", "verify", "account", "wallet", "gift", "free", "bonus",
            "secure", "update", "confirm", "password", "payment", "admin",
            "signin", "reset", "otp", "token", "invoice", "billing", "crypto", "airdrop"
        };

        // Các đường dẫn nhạy cảm liên quan đến quản trị hệ thống, có thể là mục tiêu tấn công hoặc dò quét lỗi.
        private static readonly string[] DangerousPathWords =
        {
            "wp-admin", "phpmyadmin", "cpanel", "shell", "cmd"
        };

        // Các đuôi tên miền (TLD) miễn phí hoặc giá rẻ, thường bị kẻ xấu lợi dụng để tạo trang web lừa đảo.
        private static readonly string[] RiskyTlds =
        {
            ".xyz", ".top", ".click", ".info", ".shop", ".online", ".work",
            ".rest", ".site", ".live", ".vip", ".club", ".buzz", ".icu", ".cyou", ".monster"
        };

        // Các đuôi tên miền phụ cấp 2 phổ biến của Việt Nam dùng để phân tách tên miền chính xác.
        private static readonly string[] VietnameseSecondLevelTlds =
        {
            ".com.vn", ".net.vn", ".org.vn", ".gov.vn", ".edu.vn",
            ".biz.vn", ".info.vn", ".name.vn", ".pro.vn", ".health.vn"
        };

        private readonly ApplicationDbContext _context;
        private readonly ContentCategorizationService _categorizationService;
        private readonly GoogleSafeBrowsingService _safeBrowsingService;

        public UrlScannerService(
            ApplicationDbContext context,
            ContentCategorizationService categorizationService,
            GoogleSafeBrowsingService safeBrowsingService)
        {
            _context = context;
            _categorizationService = categorizationService;
            _safeBrowsingService = safeBrowsingService;
        }

        /// <summary>
        /// Thực hiện quét toàn bộ thông tin của URL bao gồm định vị địa lý (geolocation),
        /// kiểm tra trạng thái HTTP phản hồi, đối chiếu blacklist, và phân tích rủi ro.
        /// </summary>
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
            string? htmlContent = null;
            try
            {
                uri = new Uri(url);
                result.NormalizedDomain = DomainHelper.NormalizeHost(uri.Host);
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
                // Nếu phân tích URL hoặc định vị địa lý lỗi, trả về giá trị mặc định tránh làm đứt luồng.
                result.IpAddress = "-";
                result.CountryName = "Unknown";
                result.CountryCode = "-";
                result.City = "Unknown";
                result.Isp = "Unknown";
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // LƯU Ý KHI CHẠY TẢI CAO (CONCURRENCY):
                // Việc tạo mới HttpClient liên tục qua mỗi request (using var httpClient = new HttpClient) 
                // có thể gây cạn kiệt cổng kết nối (Socket Exhaustion) trên OS khi có hàng ngàn người dùng quét cùng lúc.
                // Để tối ưu hóa chạy tải cao, khuyến nghị cấu hình IHttpClientFactory trong Program.cs để tái sử dụng socket.
                
                // Tắt tự động chuyển hướng (AllowAutoRedirect = false) để phát hiện hành vi redirect của URL.
                var handler = new HttpClientHandler { AllowAutoRedirect = false };
                using var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NetURLScanner/1.0");

                var response = await httpClient.GetAsync(url);
                stopwatch.Stop();

                int statusCodeVal = (int)response.StatusCode;
                result.StatusCode = statusCodeVal;
                result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;

                if (response.IsSuccessStatusCode)
                {
                    var mediaType = response.Content.Headers.ContentType?.MediaType ?? "";
                    if (mediaType.Contains("html", StringComparison.OrdinalIgnoreCase) ||
                        mediaType.Contains("text", StringComparison.OrdinalIgnoreCase) ||
                        string.IsNullOrEmpty(mediaType))
                    {
                        var bytes = await response.Content.ReadAsByteArrayAsync();
                        if (bytes.Length > 0 && bytes.Length <= 512_000)
                            htmlContent = Encoding.UTF8.GetString(bytes);
                    }
                }

                // Phân loại trạng thái dựa trên dải mã lỗi HTTP chuẩn.
                result.Status = response.IsSuccessStatusCode ? "Online"
                    : statusCodeVal is >= 300 and < 400 ? "Redirect"
                    : statusCodeVal is >= 400 and < 500 ? "Client Error"
                    : statusCodeVal >= 500 ? "Server Error"
                    : "Warning";
            }
            catch (Exception ex)
            {
                // Hợp nhất các catch block cũ để mã nguồn gọn gàng.
                // Nếu Timeout (TaskCanceledException) thì dùng thông báo tiếng Việt thân thiện, ngược lại lấy message lỗi ngoại lệ.
                stopwatch.Stop();
                result.Status = "Offline";
                result.ErrorMessage = ex is TaskCanceledException ? "Timeout - URL phản hồi quá lâu" : ex.Message;
                result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            }

            var trustedBrands = await GetTrustedBrandsAsync();
            var blacklistResult = await CheckBlacklist(uri?.Host ?? string.Empty);
            var risk = AnalyzeRisk(url, result, trustedBrands, blacklistResult);

            result.RiskScore = risk.Score;
            result.RiskLevel = risk.Level;
            result.Reasons = string.Join("; ", risk.Reasons);

            if (!string.IsNullOrWhiteSpace(htmlContent))
            {
                try
                {
                    var category = _categorizationService.Categorize(htmlContent, url);
                    result.SiteCategory = category.PrimaryCategory;
                    result.SiteCategoryTags = string.Join(", ", category.Tags);
                }
                catch
                {
                    result.SiteCategory = "Không phân loại được";
                }
            }

            var safeBrowsing = await _safeBrowsingService.CheckUrlAsync(url);
            result.SafeBrowsingStatus = safeBrowsing.Status;
            result.SafeBrowsingThreatType = safeBrowsing.ThreatType;

            return result;
        }

        /// <summary>
        /// Lấy danh sách thương hiệu đáng tin cậy bao gồm các cấu hình tĩnh mặc định và từ DB.
        /// LƯU Ý TẢI CAO: Việc query trực tiếp cơ sở dữ liệu trên mỗi request quét URL (1000 lượt quét = 1000 lượt SELECT) 
        /// sẽ gây nghẽn cổ chai DB. Khuyến nghị lưu danh sách này vào bộ nhớ Cache (MemoryCache/Redis) để tăng hiệu năng chịu tải.
        /// </summary>
        private async Task<Dictionary<string, string>> GetTrustedBrandsAsync()
        {
            var trustedBrands = TrustedBrandDefaults.GetDefaultBrands();

            var databaseBrands = await _context.TrustedBrands
                .AsNoTracking()
                .Where(x => x.IsActive)
                .ToListAsync();

            // Đơn giản hóa: Dùng trực tiếp cơ chế ghi đè hoặc thêm mới của dictionary [key] = value, loại bỏ kiểm tra ContainsKey dư thừa.
            foreach (var brand in databaseBrands)
            {
                string brandName = brand.BrandName.Trim().ToLower();
                string domain = brand.OfficialDomain.Trim().ToLower();

                if (!string.IsNullOrWhiteSpace(brandName) && !string.IsNullOrWhiteSpace(domain))
                {
                    trustedBrands[brandName] = domain;
                }
            }

            return trustedBrands;
        }

        /// <summary>
        /// Đối chiếu tên miền quét với cơ sở dữ liệu các tên miền nằm trong Blacklist của hệ thống.
        /// LƯU Ý TẢI CAO: Tránh truy vấn bảng Blacklist trực tiếp từ DB cho từng lượt quét URL riêng biệt để hạn chế nghẽn DB. 
        /// Nên đưa danh sách Blacklist này vào bộ nhớ đệm Cache và cập nhật định kỳ.
        /// </summary>
        private async Task<(bool IsBlacklisted, string Category, string Severity, string Reason)> CheckBlacklist(string host)
        {
            // Thực hiện tải danh sách hoạt động về bộ nhớ để tránh lỗi biên dịch truy vấn SQL phức tạp (EndsWith/Trim/ToLower).
            var blacklistedDomains = await _context.BlacklistedDomains
                .AsNoTracking()
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

        /// <summary>
        /// Chuẩn hóa URL để luôn có tiền tố giao thức rõ ràng.
        /// </summary>
        private string NormalizeUrl(string url)
        {
            url = url.Trim();
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }
            return url;
        }

        /// <summary>
        /// Phân tích rủi ro dựa trên nhiều tiêu chí: HTTPS, Trạng thái phản hồi, cấu trúc URL,
        /// ký tự lạ (homograph/punycode), từ khóa nhạy cảm, giả mạo thương hiệu và đuôi tên miền rủi ro.
        /// </summary>
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

            // Nếu nằm trong Blacklist, trả về điểm tối đa 100 ngay lập tức.
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

            // Kiểm tra xem tên miền hiện tại có phải là tên miền chính thức của một thương hiệu đáng tin cậy hay không.
            bool isOfficialTrustedDomain = trustedBrands.Values.Any(domain =>
                host == domain || host.EndsWith("." + domain));

            // 1. Nhóm kết nối & phản hồi (Giới hạn tối đa 45 điểm)
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

            // 2. Hiệu năng phản hồi (Giới hạn tối đa 15 điểm)
            if (scan.ResponseTimeMs > 7000)
            {
                AddCategoryScore(categoryScores, reasons, "performance", 15, "Thời gian phản hồi rất chậm (> 7 giây)", 15);
            }
            else if (scan.ResponseTimeMs > 3000)
            {
                AddCategoryScore(categoryScores, reasons, "performance", 8, "Thời gian phản hồi chậm (> 3 giây)", 15);
            }

            // 3. Cấu trúc URL (Giới hạn tối đa 35 điểm)
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
                AddCategoryScore(categoryScores, reasons, "structure", 10, "Domain có nhiều dấu gạch ngang", 35);
            }

            if (IPAddress.TryParse(host, out _))
            {
                AddCategoryScore(categoryScores, reasons, "structure", 18, "URL sử dụng địa chỉ IP thay vì domain", 35);
            }

            if (!uri.IsDefaultPort && !CommonPorts.Contains(uri.Port))
            {
                AddCategoryScore(categoryScores, reasons, "structure", 8, $"URL sử dụng port không phổ biến: {uri.Port}", 35);
            }

            if (CountSubdomains(host) >= 3)
            {
                AddCategoryScore(categoryScores, reasons, "structure", 12, "Domain có quá nhiều subdomain", 35);
            }

            // 4. Ký tự lạ Homograph / Punycode giả mạo ký tự ASCII thường gặp (Giới hạn tối đa 30 điểm)
            if (HasNonAsciiCharacter(host))
            {
                AddCategoryScore(categoryScores, reasons, "spoofing", 28, "Domain chứa ký tự Unicode bất thường, có thể là homograph attack", 30);
            }
            else if (host.Contains("xn--"))
            {
                AddCategoryScore(categoryScores, reasons, "spoofing", 22, "Domain sử dụng punycode, có thể giả mạo ký tự", 30);
            }

            // 5. Kiểm tra từ khóa cờ bạc / cá cược
            string? gamblingMatch = GamblingWords.FirstOrDefault(word => lowerUrl.Contains(word));
            bool hasGamblingKeyword = gamblingMatch != null;

            if (hasGamblingKeyword)
            {
                categoryScores["gambling"] = 55;
                reasons.Add($"URL chứa từ khóa liên quan đến cá cược/cờ bạc: {gamblingMatch}");
                reasons.Add("URL thuộc nhóm nội dung rủi ro cao, có thể tiềm ẩn nguy cơ mất tài sản, lừa đảo tài chính hoặc thu thập thông tin cá nhân.");

                if (FinancialActionWords.Any(word => lowerUrl.Contains(word)))
                {
                    categoryScores["gambling"] = 70;
                    reasons.Add("URL cá cược/cờ bạc có liên quan đến đăng nhập, tài khoản, nạp tiền, rút tiền hoặc giao dịch tài chính.");
                }
            }

            // 6. Nhận dạng các từ khóa đáng ngờ khác khi không phải là domain chính thức của thương hiệu tin cậy.
            if (!isOfficialTrustedDomain)
            {
                // Giới hạn kiểm tra tối đa 3 từ khóa đáng ngờ để tránh bão lý do (Capped ở 24 điểm, 8 điểm/từ)
                int suspiciousCount = 0;
                foreach (var word in SuspiciousWords)
                {
                    if (ContainsKeyword(lowerUrl, word))
                    {
                        AddCategoryScore(categoryScores, reasons, "keywords", 8, $"URL chứa từ khóa đáng ngờ: {word}", 24);
                        if (++suspiciousCount >= 3)
                        {
                            break;
                        }
                    }
                }

                foreach (var word in DangerousPathWords)
                {
                    if (ContainsPathSegment(path, word))
                    {
                        AddCategoryScore(categoryScores, reasons, "path", 12, $"Đường dẫn chứa từ khóa nhạy cảm: {word}", 24);
                    }
                }
            }

            // 7. Giả mạo thương hiệu: Lấy kết quả khớp có điểm số nguy cơ cao nhất (Giới hạn tối đa 40 điểm)
            var brandRisk = CheckBrandImpersonation(host, url, trustedBrands);
            if (brandRisk.Score > 0)
            {
                categoryScores["brand"] = Math.Min(brandRisk.Score, 40);
                reasons.AddRange(brandRisk.Reasons);
            }

            // 8. Đuôi tên miền (TLD) có độ uy tín thấp
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

        /// <summary>
        /// Cộng điểm rủi ro vào một nhóm (category) cụ thể và ghi nhận lý do.
        /// Giới hạn điểm của mỗi nhóm không vượt quá giá trị 'cap' quy định.
        /// </summary>
        private static void AddCategoryScore(
            Dictionary<string, int> categories,
            List<string> reasons,
            string category,
            int points,
            string reason,
            int cap)
        {
            int current = categories.GetValueOrDefault(category);
            if (current >= cap)
            {
                return;
            }

            categories[category] = Math.Min(current + points, cap);
            reasons.Add(reason);
        }

        /// <summary>
        /// Xác định mức độ rủi ro dựa trên điểm tổng hợp và các đặc trưng rủi ro cao đặc thù (cá cược, giả mạo thương hiệu nặng).
        /// </summary>
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

            return score switch
            {
                <= 25 => "Safe",
                <= 55 => "Warning",
                _ => "Suspicious"
            };
        }

        /// <summary>
        /// Phân tích giả mạo thương hiệu: Kiểm tra xem tên miền có bắt chước các thương hiệu uy tín bằng các thủ thuật như:
        /// thay thế ký tự nhìn giống nhau (l0gin thay vì login), chèn tên thương hiệu vào tham số URL, hay khoảng cách Levenshtein nhỏ.
        /// </summary>
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

            // Lặp qua các thương hiệu tin cậy để so sánh độ tương đồng.
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

                // Trường hợp 1: Tên miền sau khi chuẩn hóa ký tự lừa thị giác (lookalike) trùng với thương hiệu gốc.
                if (normalizedMainName == brandName && mainName != brandName)
                {
                    matchScore = Math.Max(matchScore, 32);
                    matchReasons.Add($"Domain dùng ký tự thay thế để giống {brandName}");
                }

                // Trường hợp 2: Chứa tên thương hiệu trong URL (path/query) nhưng tên miền chính lại không trùng khớp.
                if (lowerFullUrl.Contains(brandName) && !host.Contains(brandName))
                {
                    matchScore = Math.Max(matchScore, 35);
                    matchReasons.Add($"URL chứa tên thương hiệu {brandName} trong đường dẫn hoặc tham số nhưng domain thật không phải domain chính thức");
                }

                // Trường hợp 3: Host chứa toàn bộ domain chính thức nhưng lại có thêm đuôi lạ (ví dụ: google.com.scammer.com).
                if (host.Contains(officialDomain))
                {
                    matchScore = Math.Max(matchScore, 35);
                    matchReasons.Add($"Domain chứa {officialDomain} nhưng không phải domain chính thức");
                }

                bool isShortBrand = brandName.Length <= 3;

                // Trường hợp 4: Thương hiệu dài và có tên miền phụ chứa thương hiệu.
                if (!isShortBrand && mainName.Contains(brandName) && host != officialDomain)
                {
                    matchScore = Math.Max(matchScore, 25);
                    matchReasons.Add($"Domain có chứa tên thương hiệu {brandName} nhưng không phải domain chính thức");
                }

                // Trường hợp 5: Thương hiệu ngắn trùng khít với tên miền chính nhưng không thuộc domain gốc.
                if (isShortBrand && mainName == brandName && host != officialDomain)
                {
                    matchScore = Math.Max(matchScore, 25);
                    matchReasons.Add($"Domain sử dụng tên viết tắt {brandName} nhưng không phải domain chính thức");
                }

                // Trường hợp 6: Khoảng cách Levenshtein (edit distance) lệch nhau dưới 2 ký tự (sai chính tả nhẹ như faceb00k).
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

                // Giữ lại kết quả quét rủi ro nhất giữa các thương hiệu.
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

        /// <summary>
        /// Đánh giá rủi ro từ các đuôi miền (TLD). Nếu có các dấu hiệu đáng ngờ khác trong categories, rủi ro sẽ tăng cao hơn.
        /// </summary>
        private RiskResult CheckTldRisk(string host, Dictionary<string, int> categoryScores)
        {
            if (!RiskyTlds.Any(tld => host.EndsWith(tld)))
            {
                return new RiskResult { Score = 0, Level = "", Reasons = new List<string>() };
            }

            // Tính tổng các chỉ số rủi ro phi TLD. Nếu đã có rủi ro (> 15 điểm), nâng điểm phạt TLD từ 5 lên 12.
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

        /// <summary>
        /// Kiểm tra từ khóa có nằm độc lập trong văn bản theo các ranh giới từ phân tách bằng dấu phân tách URL đặc trưng.
        /// </summary>
        private static bool ContainsKeyword(string text, string keyword)
        {
            // Regex khớp từ khóa nằm ở rìa chuỗi hoặc bao quanh bởi các ký tự /, ?, &, =, -, _, .
            string pattern = $@"(^|[/\?&=\-_.]){Regex.Escape(keyword)}([/\?&=\-_.]|$)";
            return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Kiểm tra từ khóa có tồn tại như là một phân đoạn đường dẫn thư mục URL (URL Path Segment) độc lập.
        /// </summary>
        private static bool ContainsPathSegment(string path, string segment)
        {
            return path.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Any(part => part.Equals(segment, StringComparison.OrdinalIgnoreCase)
                          || part.StartsWith(segment + "-", StringComparison.OrdinalIgnoreCase)
                          || part.EndsWith("-" + segment, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Đếm số lượng Subdomain phụ của Host (bỏ qua tên miền chính và TLD cơ sở).
        /// </summary>
        private static int CountSubdomains(string host)
        {
            var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
            return Math.Max(0, parts.Length - 2);
        }

        /// <summary>
        /// Trích xuất tên miền chính (Second-Level Domain) từ host, hỗ trợ bỏ qua các TLD đặc trưng của Việt Nam.
        /// Ví dụ: `sub.vietcombank.com.vn` -> `vietcombank`.
        /// </summary>
        private static string GetMainDomainName(string host)
        {
            host = host.ToLower().Trim();

            foreach (var tld in VietnameseSecondLevelTlds)
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

        private static bool HasNonAsciiCharacter(string text) => text.Any(c => c > 127);

        /// <summary>
        /// Thay thế các ký tự lừa thị giác phổ biến (lookalike characters) về dạng chuẩn để đối chiếu giả mạo.
        /// </summary>
        private static string NormalizeLookalike(string input)
        {
            return input.ToLower()
                .Replace("0", "o")
                .Replace("1", "l")
                .Replace("3", "e")
                .Replace("5", "s")
                .Replace("@", "a")
                .Replace("$", "s");
        }

        /// <summary>
        /// Thuật toán quy hoạch động tính toán khoảng cách Levenshtein để phát hiện các tên miền cố tình sai lỗi chính tả nhẹ.
        /// </summary>
        private static int LevenshteinDistance(string a, string b)
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

        /// <summary>
        /// Gọi API định vị địa lý bên ngoài dựa trên địa chỉ IPv4 giải quyết được từ DNS của Host.
        /// LƯU Ý TẢI CAO: API 'ip-api.com' miễn phí giới hạn tần suất 45 requests/phút. 
        /// Khi có hàng ngàn lượt quét đồng thời, API này sẽ chặn yêu cầu và trả về lỗi, hệ thống sẽ tự động gán vị trí mặc định 'Unknown'.
        /// Nếu đưa vào sản phẩm lớn, cần mua gói dịch vụ trả phí (Pro) hoặc sử dụng cơ sở dữ liệu MaxMind GeoIP offline.
        /// </summary>
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

                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var response = await client.GetAsync($"http://ip-api.com/json/{ipv4}");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadFromJsonAsync<IpApiResponse>();
                    if (json is { status: "success" })
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
