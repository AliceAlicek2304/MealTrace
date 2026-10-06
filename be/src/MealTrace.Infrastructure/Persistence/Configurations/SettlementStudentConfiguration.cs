using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class SettlementStudentConfiguration : IEntityTypeConfiguration<SettlementStudent>
{
    public void Configure(EntityTypeBuilder<SettlementStudent> builder)
    {
        builder.HasKey(x => new { x.PortionSettlementId, x.StudentId });
        builder.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
    }
}
