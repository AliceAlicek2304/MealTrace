using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class SupplierRevisionConfiguration : IEntityTypeConfiguration<SupplierRevision>
{
    public void Configure(EntityTypeBuilder<SupplierRevision> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Contact).HasMaxLength(500);
        builder.Property(x => x.Reference).HasMaxLength(100);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.HasIndex(x => new { x.SupplierId, x.Version }).IsUnique();
        builder.HasIndex(x => x.PriorVersionId).IsUnique();
        builder.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SupplierRevision>().WithMany().HasForeignKey(x => x.PriorVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_SupplierRevisions_Version", "\"Version\" >= 1");
        });
    }
}
