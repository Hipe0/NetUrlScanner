using Microsoft.EntityFrameworkCore;
using NetURLScanner.Models;

namespace NetURLScanner.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<UrlScan> UrlScans { get; set; }
        public DbSet<TrustedBrand> TrustedBrands { get; set; }
        public DbSet<BlacklistedDomain> BlacklistedDomains { get; set; }
        public DbSet<BlacklistedBankAccount> BlacklistedBankAccounts { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<ScamReport> ScamReports { get; set; }
        public DbSet<FaqItem> FaqItems { get; set; }
        public DbSet<SiteContent> SiteContents { get; set; }
        public DbSet<SiteFeedback> SiteFeedbacks { get; set; }
        public DbSet<DomainVote> DomainVotes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TrustedBrand>()
                .HasIndex(x => x.OfficialDomain)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(x => x.Email)
                .IsUnique();

            modelBuilder.Entity<SiteContent>()
                .HasIndex(x => x.ContentKey)
                .IsUnique();

            modelBuilder.Entity<DomainVote>()
                .HasIndex(x => new { x.UserId, x.NormalizedDomain })
                .IsUnique();

            modelBuilder.Entity<DomainVote>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
