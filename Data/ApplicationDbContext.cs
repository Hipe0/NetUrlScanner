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
    }
}