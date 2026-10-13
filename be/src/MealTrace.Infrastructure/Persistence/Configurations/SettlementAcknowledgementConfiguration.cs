using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealTrace.Infrastructure.Persistence.Configurations;

internal class SettlementAcknowledgementConfiguration : IEntityTypeConfiguration<SettlementAcknowledgement>
{
    public void Configure(EntityTypeBuilder<SettlementAcknowledgement> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.MealDayId, x.AcknowledgedAt });
        builder.Property(x => x.AcknowledgedBy).HasMaxLength(128);
        builder.Property(x => x.AcknowledgedByName).HasMaxLength(256);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasOne(x => x.MealDay)
            .WithMany(x => x.Acknowledgements)
            .HasForeignKey(x => x.MealDayId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
