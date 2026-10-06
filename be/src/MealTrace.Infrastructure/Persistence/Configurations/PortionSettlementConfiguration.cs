using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class PortionSettlementConfiguration : IEntityTypeConfiguration<PortionSettlement>
{
    public void Configure(EntityTypeBuilder<PortionSettlement> builder)
    {
        builder.HasIndex(x => new { x.MealDayId, x.SettledAt });
        builder.Property(x => x.Version).HasDefaultValue(1);
        builder.HasIndex(x => new { x.MealDayId, x.ClassId, x.Version }).IsUnique().HasFilter("\"ClassId\" IS NOT NULL");
        builder.HasOne<PortionSettlement>().WithMany().HasForeignKey(x => x.SupersedesId).OnDelete(DeleteBehavior.Restrict);
    }
}
