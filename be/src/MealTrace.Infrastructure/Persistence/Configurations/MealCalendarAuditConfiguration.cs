using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class MealCalendarAuditConfiguration : IEntityTypeConfiguration<MealCalendarAudit>
{
    public void Configure(EntityTypeBuilder<MealCalendarAudit> builder)
    {
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.HasIndex(x => new { x.SchoolYear, x.RecordedAt });
    }
}
