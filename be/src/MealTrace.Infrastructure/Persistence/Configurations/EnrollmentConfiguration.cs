using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.HasIndex(x => new { x.StudentId, x.StartDate }).IsUnique();
        builder.HasIndex(x => new { x.ClassId, x.StartDate, x.EndDate });
        builder.HasIndex(x => x.StudentId).IsUnique().HasFilter("\"EndDate\" IS NULL");
        builder.ToTable(t => t.HasCheckConstraint("CK_Enrollment_Dates", "\"EndDate\" IS NULL OR \"EndDate\" > \"StartDate\""));
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.EndReason).HasMaxLength(500);
        builder.HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.EndedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
