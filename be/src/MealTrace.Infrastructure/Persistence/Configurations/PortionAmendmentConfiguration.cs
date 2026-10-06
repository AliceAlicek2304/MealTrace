using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class PortionAmendmentConfiguration : IEntityTypeConfiguration<PortionAmendment>
{
    public void Configure(EntityTypeBuilder<PortionAmendment> builder)
    {
        builder.HasOne(x => x.BaseSettlement).WithMany().HasForeignKey(x => x.BaseSettlementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.BaseSettlementId, x.RequestedAt });
        builder.Property(x => x.Reason).HasMaxLength(500);
    }
}
