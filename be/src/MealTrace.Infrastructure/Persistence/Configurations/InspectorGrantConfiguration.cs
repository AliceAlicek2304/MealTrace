using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class InspectorGrantConfiguration : IEntityTypeConfiguration<InspectorGrant>
{
    public void Configure(EntityTypeBuilder<InspectorGrant> builder)
    {
        builder.HasKey(x => x.UserId);
        builder.HasOne<ApplicationUser>().WithOne(x => x.InspectorGrant).HasForeignKey<InspectorGrant>(x => x.UserId);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.GrantedById).OnDelete(DeleteBehavior.Restrict);
    }
}
