using KhabzelaConsulting.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KhabzelaConsulting.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Client> Clients => Set<Client>();
        public DbSet<Service> Services => Set<Service>();
        public DbSet<PricingRule> PricingRules => Set<PricingRule>();
        public DbSet<Quote> Quotes => Set<Quote>();
        public DbSet<QuoteLine> QuoteLines => Set<QuoteLine>();
        public DbSet<Appointment> Appointments => Set<Appointment>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<Invoice> Invoices => Set<Invoice>();
        public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

            b.Entity<Client>(e =>
            {
                e.HasIndex(c => c.UserId).IsUnique();
                e.HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(c => c.Accountant).WithMany().HasForeignKey(c => c.AccountantId).OnDelete(DeleteBehavior.NoAction);
            });

            b.Entity<Service>().HasIndex(s => s.Key).IsUnique();

            b.Entity<PricingRule>(e =>
            {
                e.HasIndex(r => new { r.ServiceId, r.Key }).IsUnique();
                e.Property(r => r.MinValue).HasPrecision(12, 2);
                e.Property(r => r.MaxValue).HasPrecision(12, 2);
            });

            b.Entity<Quote>(e =>
            {
                e.Property(q => q.EstimateLow).HasPrecision(12, 2);
                e.Property(q => q.EstimateHigh).HasPrecision(12, 2);
                e.HasIndex(q => q.Status);
            });

            b.Entity<QuoteLine>().HasOne(l => l.Service).WithMany().HasForeignKey(l => l.ServiceId).OnDelete(DeleteBehavior.Restrict);

            b.Entity<Appointment>(e =>
            {
                e.HasOne(a => a.Quote).WithOne(q => q.Appointment).HasForeignKey<Appointment>(a => a.QuoteId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(a => a.Client).WithMany(c => c.Appointments).HasForeignKey(a => a.ClientId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(a => a.Consultant).WithMany().HasForeignKey(a => a.ConsultantId).OnDelete(DeleteBehavior.NoAction);
                e.HasIndex(a => new { a.ConsultantId, a.ScheduledAt });
            });

            b.Entity<Notification>(e =>
            {
                e.HasIndex(n => new { n.ClientId, n.ReadAt });
                e.HasIndex(n => new { n.ClientId, n.DedupKey }).IsUnique();
            });

            b.Entity<Invoice>(e =>
            {
                e.HasIndex(i => i.Number).IsUnique();
                e.Property(i => i.Amount).HasPrecision(12, 2);
            });
        }
    }

    // Used by `dotnet ef migrations add` — migrations target SQL Server (Azure SQL in production).
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer("Server=design-time;Database=Khabzela;Trusted_Connection=False;")
                .Options;
            return new AppDbContext(options);
        }
    }
}
