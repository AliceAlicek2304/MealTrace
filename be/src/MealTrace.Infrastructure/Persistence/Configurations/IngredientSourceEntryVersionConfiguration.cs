using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class IngredientSourceEntryVersionConfiguration : IEntityTypeConfiguration<IngredientSourceEntryVersion>
{
    public void Configure(EntityTypeBuilder<IngredientSourceEntryVersion> builder)
    {
        builder.Property(x => x.Quantity).HasPrecision(14, 3);
        builder.Property(x => x.Unit).HasMaxLength(10);
        builder.Property(x => x.DeliveryRef).HasMaxLength(100);
        builder.Property(x => x.LotRef).HasMaxLength(100);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.HasIndex(x => new { x.SourceEntryId, x.Version }).IsUnique();
        builder.HasIndex(x => x.PriorVersionId).IsUnique();
        builder.HasIndex(x => x.ReferenceDate);
        builder.HasOne<IngredientSourceEntry>().WithMany().HasForeignKey(x => x.SourceEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SupplierRevision>().WithMany().HasForeignKey(x => x.SupplierRevisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<IngredientSourceEntryVersion>().WithMany().HasForeignKey(x => x.PriorVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_IngredientSourceEntryVersions_Quantity", "\"Quantity\" > 0");
            t.HasCheckConstraint("CK_IngredientSourceEntryVersions_Unit", "\"Unit\" IN ('g', 'ml')");
            t.HasCheckConstraint("CK_IngredientSourceEntryVersions_Version", "\"Version\" >= 1");
        });
    }
}
