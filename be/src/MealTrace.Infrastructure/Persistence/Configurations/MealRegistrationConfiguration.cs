using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class MealRegistrationConfiguration : IEntityTypeConfiguration<MealRegistration>
{
    public void Configure(EntityTypeBuilder<MealRegistration> builder)
    {
        builder.HasIndex(x => new { x.MealDayId, x.StudentId, x.RecordedAt });
        builder.HasIndex(x => new { x.MealDayId, x.StudentId, x.Sequence }).IsUnique();
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.RecordedByName).HasMaxLength(120);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
