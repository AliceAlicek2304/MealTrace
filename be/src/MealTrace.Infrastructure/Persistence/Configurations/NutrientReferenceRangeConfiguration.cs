using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class NutrientReferenceRangeConfiguration : IEntityTypeConfiguration<NutrientReferenceRange>
{
    public void Configure(EntityTypeBuilder<NutrientReferenceRange> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.AgeGroup, x.Nutrient });
        builder.Property(x => x.AgeGroup).HasMaxLength(50);
        builder.Property(x => x.Nutrient).HasMaxLength(20);
        builder.Property(x => x.MinValue).HasPrecision(12, 3);
        builder.Property(x => x.MaxValue).HasPrecision(12, 3);
        builder.Property(x => x.Source).HasMaxLength(200);
        builder.Property(x => x.SourceRef).HasMaxLength(500);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_NutrientReferenceRanges_Nutrient", "\"Nutrient\" IN ('kcal', 'proteinG', 'fatG', 'carbG')");
            t.HasCheckConstraint("CK_NutrientReferenceRanges_Bounds", "\"MinValue\" IS NOT NULL OR \"MaxValue\" IS NOT NULL");
            t.HasCheckConstraint("CK_NutrientReferenceRanges_Order", "\"MinValue\" IS NULL OR \"MaxValue\" IS NULL OR \"MinValue\" <= \"MaxValue\"");
            t.HasCheckConstraint("CK_NutrientReferenceRanges_Interval", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" > \"EffectiveFrom\"");
        });
    }
}
