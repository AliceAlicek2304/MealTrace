using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class ReportSnapshotConfiguration : IEntityTypeConfiguration<ReportSnapshot>
{
    public void Configure(EntityTypeBuilder<ReportSnapshot> builder)
    {
        builder.Property(x => x.EnergyKcalPerPortion).HasPrecision(12, 3);
        builder.Property(x => x.CostPerPortion).HasPrecision(14, 2);
        builder.Property(x => x.SourceJson).HasColumnType("jsonb");
    }
}
