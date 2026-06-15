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

        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TrustedBrand>()
                .HasIndex(x => x.OfficialDomain)
                .IsUnique();
        }
    }
}