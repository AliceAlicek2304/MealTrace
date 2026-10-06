using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class MealCalendarExceptionConfiguration : IEntityTypeConfiguration<MealCalendarException>
{
    public void Configure(EntityTypeBuilder<MealCalendarException> builder)
    {
        builder.HasKey(x => new { x.SchoolYear, x.Date });
        builder.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.SchoolYear).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Reason).HasMaxLength(500);
    }
}
