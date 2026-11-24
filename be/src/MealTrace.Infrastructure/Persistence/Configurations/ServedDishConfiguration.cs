using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class ServedDishConfiguration : IEntityTypeConfiguration<ServedDish>
{
    public void Configure(EntityTypeBuilder<ServedDish> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SubstitutionReason).HasMaxLength(500);
        builder.HasMany(x => x.IngredientUses).WithOne(x => x.ServedDish).HasForeignKey(x => x.ServedDishId).OnDelete(DeleteBehavior.Cascade);
    }
}
