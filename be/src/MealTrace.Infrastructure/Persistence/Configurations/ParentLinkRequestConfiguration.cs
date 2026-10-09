using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class ParentLinkRequestConfiguration : IEntityTypeConfiguration<ParentLinkRequest>
{
    public void Configure(EntityTypeBuilder<ParentLinkRequest> builder)
    {
        builder.Property(x => x.StudentCode).HasMaxLength(40);
        builder.Property(x => x.StudentName).HasMaxLength(150);
        builder.Property(x => x.Relationship).HasMaxLength(20);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.ReviewReason).HasMaxLength(500);
        builder.Property(x => x.RevocationReason).HasMaxLength(500);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RevokedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.Property(x => x.Revision).IsConcurrencyToken();
        builder.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ParentId, x.StudentId }).IsUnique().HasFilter("\"Status\" = 'PENDING'");
        builder.HasIndex(x => new { x.ParentId, x.RequestedAt });
        builder.HasIndex(x => new { x.Status, x.RequestedAt });
    }
}
