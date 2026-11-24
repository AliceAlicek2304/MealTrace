using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class ServingConfiguration : IEntityTypeConfiguration<Serving>
{
    public void Configure(EntityTypeBuilder<Serving> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.MealDayId, x.ClassId });
        builder.HasOne(x => x.MealDay).WithMany().HasForeignKey(x => x.MealDayId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Versions).WithOne(x => x.Serving).HasForeignKey(x => x.ServingId).OnDelete(DeleteBehavior.Cascade);
    }
}
