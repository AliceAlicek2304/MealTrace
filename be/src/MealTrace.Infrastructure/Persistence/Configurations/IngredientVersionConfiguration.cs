using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class IngredientVersionConfiguration : IEntityTypeConfiguration<IngredientVersion>
{
    public void Configure(EntityTypeBuilder<IngredientVersion> builder)
    {
        builder.HasIndex(x => new { x.IngredientId, x.Version }).IsUnique();
        builder.Property(x => x.EnergyKcalPer100G).HasPrecision(12, 3);
        builder.Property(x => x.ProteinGPer100G).HasPrecision(12, 3);
        builder.Property(x => x.PricePerKg).HasPrecision(14, 2);
        builder.Property(x => x.EdibleFraction).HasPrecision(5, 4);

        // v1.3 (BE1). Nullable so rows written before v1.3 stay valid; the service requires them on new rows.
        builder.Property(x => x.FatGPer100G).HasPrecision(12, 3);
        builder.Property(x => x.CarbGPer100G).HasPrecision(12, 3);
        builder.Property(x => x.GramsPerMl).HasPrecision(8, 4);
        builder.Property(x => x.Source).HasMaxLength(200);
        builder.Property(x => x.SourceRef).HasMaxLength(500);
        // A version can be superseded only once (NULLs are not compared, so legacy rows are unaffected).
        builder.HasIndex(x => x.SupersedesId).IsUnique();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<IngredientVersion>().WithMany().HasForeignKey(x => x.SupersedesId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => t.HasCheckConstraint("CK_IngredientVersions_Interval", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" > \"EffectiveFrom\""));
    }
}
