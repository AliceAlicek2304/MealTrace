using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class IngredientEstimateConfiguration : IEntityTypeConfiguration<IngredientEstimate>
{
    public void Configure(EntityTypeBuilder<IngredientEstimate> builder)
    {
        builder.Property(x => x.PayloadHash).HasMaxLength(64);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
