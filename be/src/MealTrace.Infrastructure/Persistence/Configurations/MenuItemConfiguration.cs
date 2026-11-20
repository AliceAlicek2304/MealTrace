using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.HasIndex(x => new { x.MenuVersionId, x.RecipeVersionId }).IsUnique();
        builder.HasOne<MenuVersion>().WithMany().HasForeignKey(x => x.MenuVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RecipeVersion>().WithMany().HasForeignKey(x => x.RecipeVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_MenuItems_PlannedPortions", "\"PlannedPortions\" IS NULL OR \"PlannedPortions\" >= 1");
        });
    }
}
