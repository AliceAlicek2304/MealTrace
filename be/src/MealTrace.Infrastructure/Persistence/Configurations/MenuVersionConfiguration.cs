using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class MenuVersionConfiguration : IEntityTypeConfiguration<MenuVersion>
{
    public void Configure(EntityTypeBuilder<MenuVersion> builder)
    {
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.HasIndex(x => new { x.MenuId, x.Version }).IsUnique();
        builder.HasIndex(x => x.PriorVersionId).IsUnique();
        builder.HasOne<Menu>().WithMany().HasForeignKey(x => x.MenuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MenuVersion>().WithMany().HasForeignKey(x => x.PriorVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_MenuVersions_Version", "\"Version\" >= 1");
        });
    }
}
