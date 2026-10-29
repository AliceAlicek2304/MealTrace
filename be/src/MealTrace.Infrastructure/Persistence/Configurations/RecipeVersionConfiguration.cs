using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class RecipeVersionConfiguration : IEntityTypeConfiguration<RecipeVersion>
{
    public void Configure(EntityTypeBuilder<RecipeVersion> builder)
    {
        builder.HasIndex(x => new { x.RecipeId, x.Version }).IsUnique();

        // v1.3 (BE1). Nullable so versions written before v1.3 stay valid.
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_RecipeVersions_ServingBasis", "\"ServingBasis\" IS NULL OR \"ServingBasis\" >= 1");
            t.HasCheckConstraint("CK_RecipeVersions_Rounding",
                "(\"RoundingKcalDecimals\" IS NULL OR \"RoundingKcalDecimals\" BETWEEN 0 AND 3) AND " +
                "(\"RoundingMacroDecimals\" IS NULL OR \"RoundingMacroDecimals\" BETWEEN 0 AND 3)");
        });
    }
}
