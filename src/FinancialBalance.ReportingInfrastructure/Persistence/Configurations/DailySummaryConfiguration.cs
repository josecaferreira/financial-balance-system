using FinancialBalance.Domain.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancialBalance.ReportingInfrastructure.Persistence.Configurations;

public class DailySummaryConfiguration : IEntityTypeConfiguration<DailySummary>
{
    public void Configure(EntityTypeBuilder<DailySummary> builder)
    {
        builder.ToTable("daily_summaries");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.AccountId).IsRequired();
        builder.Property(d => d.Date).HasColumnType("date").IsRequired();

        builder.Property(d => d.TotalIncoming).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(d => d.TotalOutgoing).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(d => d.NetBalance).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(d => d.TransactionCount).IsRequired();
        builder.Property(d => d.ComputedAt).IsRequired();
        builder.Property(d => d.CreatedAt).IsRequired();
        builder.Property(d => d.UpdatedAt).IsRequired();

        builder.HasIndex(d => new { d.AccountId, d.Date }).IsUnique();

        builder.OwnsMany(d => d.CategoryBreakdowns, cb =>
        {
            cb.ToTable("daily_category_breakdowns");
            cb.HasKey(c => c.Id);
            cb.Property(c => c.Id).ValueGeneratedNever();
            cb.Property(c => c.Category).HasMaxLength(100).IsRequired();
            cb.Property(c => c.TotalIncoming).HasColumnType("numeric(18,2)");
            cb.Property(c => c.TotalOutgoing).HasColumnType("numeric(18,2)");
            cb.WithOwner().HasForeignKey("DailySummaryId");
        });
    }
}
