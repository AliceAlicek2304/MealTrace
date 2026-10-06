using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class MealScheduleConfiguration : IEntityTypeConfiguration<MealSchedule>
{
    public void Configure(EntityTypeBuilder<MealSchedule> builder)
    {
        builder.HasKey(x => x.SchoolYear);
        builder.Property(x => x.Revision).IsConcurrencyToken();
        builder.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.SchoolYear).OnDelete(DeleteBehavior.Restrict);
    }
}
