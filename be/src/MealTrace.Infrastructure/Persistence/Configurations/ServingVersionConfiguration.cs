using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class ServingVersionConfiguration : IEntityTypeConfiguration<ServingVersion>
{
    public void Configure(EntityTypeBuilder<ServingVersion> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.ServingId, x.Version }).IsUnique();
        builder.HasIndex(x => x.ClientRequestId).IsUnique().HasFilter("\"ClientRequestId\" IS NOT NULL");
        builder.Property(x => x.SnapshotToken).HasMaxLength(128);
        builder.Property(x => x.ResponsibleStaffId).HasMaxLength(64);
        builder.Property(x => x.CorrectionReason).HasMaxLength(500);
        builder.Property(x => x.RequestPayloadHash).HasMaxLength(64);
        builder.HasOne<ServingVersion>().WithMany().HasForeignKey(x => x.PriorVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.PriorVersionId).IsUnique().HasFilter("\"PriorVersionId\" IS NOT NULL");
        builder.HasMany(x => x.Dishes).WithOne(x => x.ServingVersion).HasForeignKey(x => x.ServingVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
