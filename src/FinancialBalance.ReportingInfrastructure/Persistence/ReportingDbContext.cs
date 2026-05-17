using FinancialBalance.Domain.Reporting;
using FinancialBalance.ReportingInfrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace FinancialBalance.ReportingInfrastructure.Persistence;

public class ReportingDbContext : DbContext
{
    public ReportingDbContext(DbContextOptions<ReportingDbContext> options) : base(options) { }

    public DbSet<DailySummary> DailySummaries => Set<DailySummary>();
    public DbSet<MonthlySummary> MonthlySummaries => Set<MonthlySummary>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("reporting");
        modelBuilder.ApplyConfiguration(new DailySummaryConfiguration());
        modelBuilder.ApplyConfiguration(new MonthlySummaryConfiguration());
    }
}
