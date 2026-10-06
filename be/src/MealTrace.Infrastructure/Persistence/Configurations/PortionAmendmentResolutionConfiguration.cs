using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class PortionAmendmentResolutionConfiguration : IEntityTypeConfiguration<PortionAmendmentResolution>
{
    public void Configure(EntityTypeBuilder<PortionAmendmentResolution> builder)
    {
        builder.HasKey(x => x.AmendmentId);
        builder.HasOne(x => x.Amendment).WithOne(x => x.Resolution).HasForeignKey<PortionAmendmentResolution>(x => x.AmendmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AppliedSettlement).WithMany().HasForeignKey(x => x.AppliedSettlementId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Reason).HasMaxLength(500);
    }
}
