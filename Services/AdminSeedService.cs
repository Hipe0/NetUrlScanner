using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetURLScanner.Data;
using NetURLScanner.Models;
using NetURLScanner.Options;

namespace NetURLScanner.Services;

public class AdminSeedService
{
    private readonly ApplicationDbContext _context;
    private readonly AdminSeedOptions _options;
    private readonly ILogger<AdminSeedService> _logger;

    public AdminSeedService(
        ApplicationDbContext context,
        IOptions<AdminSeedOptions> options,
        ILogger<AdminSeedService> logger)
    {
        _context = context;
        _options = options.Value;
        _logger = logger;
    }

    public string ProtectedAdminEmail => _options.Email.Trim();

    public bool IsProtectedAdmin(string? email) =>
        !string.IsNullOrWhiteSpace(email) &&
        string.Equals(email.Trim(), ProtectedAdminEmail, StringComparison.OrdinalIgnoreCase);

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await ActivateLegacyUsersAsync(cancellationToken);

        if (!_options.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.Email) || string.IsNullOrWhiteSpace(_options.Password))
        {
            _logger.LogWarning("AdminSeed bị bật nhưng thiếu Email hoặc Password trong cấu hình.");
            return;
        }

        var email = _options.Email.Trim();

        if (await _context.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return;
        }

        var hasher = new PasswordHasher<User>();
        var adminUser = new User
        {
            Email = email,
            Role = "Admin",
            IsActive = true,
            CreatedAt = DateTime.Now
        };
        adminUser.PasswordHash = hasher.HashPassword(adminUser, _options.Password);

        _context.Users.Add(adminUser);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Đã tạo tài khoản Admin mặc định: {Email}", email);
    }

    /// <summary>
    /// Kích hoạt user đã tồn tại trước migration (CreatedAt mặc định, IsActive=false).
    /// </summary>
    private async Task ActivateLegacyUsersAsync(CancellationToken cancellationToken)
    {
        var legacyUsers = await _context.Users
            .Where(u => u.CreatedAt == default)
            .ToListAsync(cancellationToken);

        if (legacyUsers.Count == 0) return;

        foreach (var user in legacyUsers)
        {
            user.IsActive = true;
            user.CreatedAt = DateTime.Now;
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Đã kích hoạt {Count} tài khoản cũ sau migration.", legacyUsers.Count);
    }
}
