using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancialBalance.Infrastructure.Persistence.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.Type).HasMaxLength(500).IsRequired();
        builder.Property(o => o.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(o => o.CreatedAt).IsRequired();

        builder.HasIndex(o => o.CreatedAt)
            .HasFilter("processed_at IS NULL");
    }
}
