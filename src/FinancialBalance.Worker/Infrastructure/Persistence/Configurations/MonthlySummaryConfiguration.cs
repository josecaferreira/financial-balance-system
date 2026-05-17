using FinancialBalance.Domain.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancialBalance.Worker.Infrastructure.Persistence.Configurations;

public class MonthlySummaryConfiguration : IEntityTypeConfiguration<MonthlySummary>
{
    public void Configure(EntityTypeBuilder<MonthlySummary> builder)
    {
        builder.ToTable("monthly_summaries");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.AccountId).IsRequired();
        builder.Property(m => m.Year).IsRequired();
        builder.Property(m => m.Month).IsRequired();
        builder.Property(m => m.OpeningBalance).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(m => m.ClosingBalance).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(m => m.TotalIncoming).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(m => m.TotalOutgoing).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(m => m.NetBalance).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(m => m.TransactionCount).IsRequired();
        builder.Property(m => m.ComputedAt).IsRequired();
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.UpdatedAt).IsRequired();

        builder.HasIndex(m => new { m.AccountId, m.Year, m.Month }).IsUnique();

        builder.OwnsMany(m => m.CategoryBreakdowns, cb =>
        {
            cb.ToTable("monthly_category_breakdowns");
            cb.HasKey(c => c.Id);
            cb.Property(c => c.Id).ValueGeneratedNever();
            cb.Property(c => c.Category).HasMaxLength(100).IsRequired();
            cb.Property(c => c.TotalIncoming).HasColumnType("numeric(18,2)");
            cb.Property(c => c.TotalOutgoing).HasColumnType("numeric(18,2)");
            cb.WithOwner().HasForeignKey("MonthlySummaryId");
        });
    }
}
