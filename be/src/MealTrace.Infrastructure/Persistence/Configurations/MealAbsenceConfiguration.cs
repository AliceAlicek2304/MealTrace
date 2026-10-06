using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class MealAbsenceConfiguration : IEntityTypeConfiguration<MealAbsence>
{
    public void Configure(EntityTypeBuilder<MealAbsence> builder)
    {
        builder.Property(x => x.SchoolYear).HasMaxLength(30);
        builder.HasIndex(x => new { x.StudentId, x.FromDate, x.ToDate });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReportedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
