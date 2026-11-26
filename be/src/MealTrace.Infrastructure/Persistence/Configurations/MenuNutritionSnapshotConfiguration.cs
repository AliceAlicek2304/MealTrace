using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class MenuNutritionSnapshotConfiguration : IEntityTypeConfiguration<MenuNutritionSnapshot>
{
    public void Configure(EntityTypeBuilder<MenuNutritionSnapshot> builder)
    {
        builder.Property(x => x.AlgorithmVersion).HasMaxLength(50);
        builder.Property(x => x.AgeGroup).HasMaxLength(50);
        builder.Property(x => x.AgeComparisonStatus).HasMaxLength(30);
        builder.Property(x => x.PayloadHash).HasMaxLength(64);
        builder.HasIndex(x => x.MenuVersionId).IsUnique();
        builder.HasOne<MenuVersion>().WithMany().HasForeignKey(x => x.MenuVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
