using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NetURLScanner.Data;
using NetURLScanner.Models;

namespace NetURLScanner.Services;

public class SampleDataSeedService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SampleDataSeedService> _logger;

    public SampleDataSeedService(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<SampleDataSeedService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var isEnabled = _configuration.GetValue<bool>("SampleDataSeed:Enabled");
        if (!isEnabled) return;

        _logger.LogInformation("Đang kiểm tra và tạo dữ liệu mẫu...");

        await SeedUsersAsync(cancellationToken);
        await SeedBlacklistedDomainsAsync(cancellationToken);
        await SeedTrustedBrandsAsync(cancellationToken);
        await SeedBlacklistedBankAccountsAsync(cancellationToken);
        await SeedScamReportsAsync(cancellationToken);
        await SeedUrlScansAsync(cancellationToken);

        _logger.LogInformation("Tạo dữ liệu mẫu hoàn tất.");
    }

    private async Task SeedUsersAsync(CancellationToken cancellationToken)
    {
        if (await _context.Users.AnyAsync(u => u.Email == "user1@gmail.com", cancellationToken)) return;

        var hasher = new PasswordHasher<User>();

        var users = new List<User>();
        for (int i = 1; i <= 5; i++)
        {
            var normalUser = new User { Email = $"user{i}@gmail.com", Role = "User", IsActive = true, IsPremium = false, CreatedAt = DateTime.Now.AddDays(-i) };
            normalUser.PasswordHash = hasher.HashPassword(normalUser, "123456");
            users.Add(normalUser);

            var premiumUser = new User { Email = $"premium{i}@gmail.com", Role = "User", IsActive = true, IsPremium = true, CreatedAt = DateTime.Now.AddDays(-i) };
            premiumUser.PasswordHash = hasher.HashPassword(premiumUser, "123456");
            users.Add(premiumUser);
        }

        _context.Users.AddRange(users);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedBlacklistedDomainsAsync(CancellationToken cancellationToken)
    {
        if (await _context.BlacklistedDomains.AnyAsync(cancellationToken)) return;

        var domains = new List<BlacklistedDomain>();
        for (int i = 1; i <= 10; i++)
        {
            domains.Add(new BlacklistedDomain
            {
                Domain = $"luradao{i}.com",
                Category = i % 2 == 0 ? "Phishing" : "Scam",
                Reason = $"Trang web giả mạo chiếm đoạt tài sản mẫu {i}",
                Severity = i % 3 == 0 ? "High" : "Medium",
                IsActive = true,
                CreatedAt = DateTime.Now.AddDays(-i)
            });
        }

        _context.BlacklistedDomains.AddRange(domains);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedTrustedBrandsAsync(CancellationToken cancellationToken)
    {
        if (await _context.TrustedBrands.AnyAsync(cancellationToken)) return;

        var brands = new List<TrustedBrand>();
        for (int i = 1; i <= 10; i++)
        {
            brands.Add(new TrustedBrand
            {
                OfficialDomain = $"chinhhang{i}.vn",
                BrandName = $"Thương hiệu Uy Tín {i}",
                Category = "Tài chính",
                IsActive = true,
                CreatedAt = DateTime.Now.AddDays(-i)
            });
        }

        _context.TrustedBrands.AddRange(brands);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedBlacklistedBankAccountsAsync(CancellationToken cancellationToken)
    {
        if (await _context.BlacklistedBankAccounts.AnyAsync(cancellationToken)) return;

        var accounts = new List<BlacklistedBankAccount>();
        var bankIds = new[] { "970436", "970422", "970415", "970418", "970407" };

        for (int i = 1; i <= 10; i++)
        {
            accounts.Add(new BlacklistedBankAccount
            {
                BankId = bankIds[i % bankIds.Length],
                BankAccountNumber = $"00001111222{i}",
                BankAccountOwnerName = $"NGUYEN VAN LUA DAO {i}",
                Reason = $"Tài khoản lừa đảo chuyển tiền cọc {i}",
                IsActive = true,
                CreatedAt = DateTime.Now.AddDays(-i)
            });
        }

        _context.BlacklistedBankAccounts.AddRange(accounts);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedScamReportsAsync(CancellationToken cancellationToken)
    {
        if (await _context.ScamReports.AnyAsync(cancellationToken)) return;

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == "user1@gmail.com", cancellationToken);
        var reports = new List<ScamReport>();
        var bankIds = new[] { "970436", "970422", "970415" };

        for (int i = 1; i <= 5; i++)
        {
            reports.Add(new ScamReport
            {
                ReportType = "Url",
                UrlOrIp = $"http://giamao{i}.com",
                Evidence = $"Tôi đã bị trang này lừa mất tiền mẫu {i}",
                AmountLost = i * 1000000,
                UserId = user?.Id,
                Status = i % 2 == 0 ? "Approved" : "Pending",
                CreatedAt = DateTime.Now.AddDays(-i)
            });
        }

        for (int i = 1; i <= 5; i++)
        {
            reports.Add(new ScamReport
            {
                ReportType = "BankAccount",
                BankId = bankIds[i % bankIds.Length],
                BankAccountNumber = $"111222333{i}",
                BankAccountOwnerName = $"KE GIAN {i}",
                Evidence = $"Chuyển tiền mua hàng không giao {i}",
                AmountLost = i * 500000,
                UserId = user?.Id,
                Status = i % 2 == 0 ? "Approved" : "Pending",
                CreatedAt = DateTime.Now.AddDays(-i)
            });
        }

        _context.ScamReports.AddRange(reports);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedUrlScansAsync(CancellationToken cancellationToken)
    {
        if (await _context.UrlScans.AnyAsync(cancellationToken)) return;

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == "user1@gmail.com", cancellationToken);
        var scans = new List<UrlScan>();

        for (int i = 1; i <= 10; i++)
        {
            scans.Add(new UrlScan
            {
                Url = $"http://testscan{i}.com",
                IpAddress = $"192.168.1.{i}",
                StatusCode = 200,
                ResponseTimeMs = 150 + i * 10,
                IsHttps = true,
                RiskScore = i * 5,
                RiskLevel = i < 5 ? "Safe" : "Warning",
                City = "Hồ Chí Minh",
                CountryName = "Vietnam",
                Latitude = 10.762622,
                Longitude = 106.660172,
                ScannedAt = DateTime.Now.AddDays(-i),
                UserId = user?.Id
            });
        }

        _context.UrlScans.AddRange(scans);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
