using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class IngredientUseConfiguration : IEntityTypeConfiguration<IngredientUse>
{
    public void Configure(EntityTypeBuilder<IngredientUse> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Unit).HasMaxLength(20);
        builder.Property(x => x.WeightBasis).HasMaxLength(20);
        builder.Property(x => x.MissingReason).HasMaxLength(500);
        builder.Property(x => x.Quantity).HasPrecision(18, 4);
        builder.Property(x => x.MissingQuantity).HasPrecision(18, 4);
        builder.HasMany(x => x.Allocations).WithOne(x => x.IngredientUse).HasForeignKey(x => x.IngredientUseId).OnDelete(DeleteBehavior.Cascade);
    }
}
