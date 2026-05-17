using FinancialBalance.Domain.Reporting;
using FinancialBalance.Worker.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace FinancialBalance.Worker.Infrastructure.Persistence;

public class WorkerDbContext : DbContext
{
    public WorkerDbContext(DbContextOptions<WorkerDbContext> options) : base(options) { }

    public DbSet<DailySummary> DailySummaries => Set<DailySummary>();
    public DbSet<MonthlySummary> MonthlySummaries => Set<MonthlySummary>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("reporting");
        modelBuilder.ApplyConfiguration(new DailySummaryConfiguration());
        modelBuilder.ApplyConfiguration(new MonthlySummaryConfiguration());
    }
}
