using Microsoft.EntityFrameworkCore;
using NetURLScanner.Models;

namespace NetURLScanner.Data
{
    /// <summary>
    /// Ngữ cảnh EF Core — ánh xạ các bảng SQL Server và cấu hình index/relationship.
    /// Inject qua constructor vào Controller/Service.
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // --- Bảng nghiệp vụ chính ---
        public DbSet<UrlScan> UrlScans { get; set; }                         // Lịch sử quét URL
        public DbSet<TrustedBrand> TrustedBrands { get; set; }               // Whitelist thương hiệu
        public DbSet<BlacklistedDomain> BlacklistedDomains { get; set; }     // Blacklist domain
        public DbSet<BlacklistedBankAccount> BlacklistedBankAccounts { get; set; } // Blacklist STK NH
        public DbSet<User> Users { get; set; }                               // Tài khoản (AppUsers)
        public DbSet<ScamReport> ScamReports { get; set; }                   // Báo cáo lừa đảo
        public DbSet<FaqItem> FaqItems { get; set; }                         // FAQ
        public DbSet<SiteContent> SiteContents { get; set; }                  // CMS (intro…)
        public DbSet<SiteFeedback> SiteFeedbacks { get; set; }               // Góp ý người dùng
        public DbSet<DomainVote> DomainVotes { get; set; }                   // Vote domain theo user

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Mỗi domain whitelist chỉ xuất hiện một lần.
            modelBuilder.Entity<TrustedBrand>()
                .HasIndex(x => x.OfficialDomain)
                .IsUnique();

            // Email đăng nhập duy nhất.
            modelBuilder.Entity<User>()
                .HasIndex(x => x.Email)
                .IsUnique();

            // Mỗi key CMS (vd. intro_header) một bản ghi.
            modelBuilder.Entity<SiteContent>()
                .HasIndex(x => x.ContentKey)
                .IsUnique();

            // Mỗi user chỉ vote một lần trên một domain.
            modelBuilder.Entity<DomainVote>()
                .HasIndex(x => new { x.UserId, x.NormalizedDomain })
                .IsUnique();

            modelBuilder.Entity<DomainVote>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade); // Xóa user → xóa vote của user đó
        }
    }
}
