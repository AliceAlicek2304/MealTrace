using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class RecipeVersionConfiguration : IEntityTypeConfiguration<RecipeVersion>
{
    public void Configure(EntityTypeBuilder<RecipeVersion> builder)
    {
        builder.HasIndex(x => new { x.RecipeId, x.Version }).IsUnique();
    }
}
