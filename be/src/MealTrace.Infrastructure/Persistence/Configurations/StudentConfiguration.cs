using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.Property(x => x.StudentCode).HasMaxLength(40);
        builder.Property(x => x.Gender).HasMaxLength(10);
        builder.HasIndex(x => x.StudentCode).IsUnique();
        builder.Property(x => x.Revision).IsConcurrencyToken();
    }
}
