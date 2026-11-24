using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class SourceAllocationConfiguration : IEntityTypeConfiguration<SourceAllocation>
{
    public void Configure(EntityTypeBuilder<SourceAllocation> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Unit).HasMaxLength(20);
        builder.Property(x => x.Quantity).HasPrecision(18, 4);
    }
}
