using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class SettlementDecisionConfiguration : IEntityTypeConfiguration<SettlementDecision>
{
    public void Configure(EntityTypeBuilder<SettlementDecision> builder)
    {
        builder.HasKey(x => new { x.PortionSettlementId, x.StudentId });
        builder.HasOne(x => x.PortionSettlement).WithMany(x => x.Decisions).HasForeignKey(x => x.PortionSettlementId).OnDelete(DeleteBehavior.Restrict);
    }
}
