using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class AccountPasswordResetAuditConfiguration : IEntityTypeConfiguration<AccountPasswordResetAudit>
{
    public void Configure(EntityTypeBuilder<AccountPasswordResetAudit> builder)
    {
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.HasIndex(x => new { x.UserId, x.PerformedAt });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.PerformedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
