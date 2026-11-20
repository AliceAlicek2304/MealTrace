using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class MenuTransitionConfiguration : IEntityTypeConfiguration<MenuTransition>
{
    public void Configure(EntityTypeBuilder<MenuTransition> builder)
    {
        builder.Property(x => x.State).HasMaxLength(20);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.HasIndex(x => new { x.MenuVersionId, x.Sequence }).IsUnique();
        builder.HasOne<MenuVersion>().WithMany().HasForeignKey(x => x.MenuVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_MenuTransitions_Sequence", "\"Sequence\" >= 1");
            t.HasCheckConstraint("CK_MenuTransitions_State", "\"State\" IN ('draft', 'submitted', 'approved', 'rejected', 'published')");
        });
    }
}
