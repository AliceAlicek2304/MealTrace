using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class MealDayConfiguration : IEntityTypeConfiguration<MealDay>
{
    public void Configure(EntityTypeBuilder<MealDay> builder)
    {
        builder.Property(x => x.CancellationReason).HasMaxLength(500);
        builder.HasIndex(x => new { x.Date, x.MealType }).IsUnique();
        builder.Property(x => x.DecisionRevision).IsConcurrencyToken();
    }
}
