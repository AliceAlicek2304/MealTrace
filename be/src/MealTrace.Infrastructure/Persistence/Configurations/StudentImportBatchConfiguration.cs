using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealTrace.Infrastructure.Persistence.Configurations;

internal sealed class StudentImportBatchConfiguration : IEntityTypeConfiguration<StudentImportBatch>
{
    public void Configure(EntityTypeBuilder<StudentImportBatch> builder)
    {
        builder.Property(x => x.ClassName).HasMaxLength(100);
        builder.Property(x => x.SchoolYear).HasMaxLength(20);
        builder.Property(x => x.FileName).HasMaxLength(200);
        builder.Property(x => x.SheetName).HasMaxLength(100);
        builder.HasIndex(x => new { x.ImportedAt, x.Id });
        builder.HasIndex(x => new { x.ClassId, x.ImportedAt });
        builder.HasOne<SchoolClass>().WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ImportedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StudentImportBatchItemConfiguration : IEntityTypeConfiguration<StudentImportBatchItem>
{
    public void Configure(EntityTypeBuilder<StudentImportBatchItem> builder)
    {
        builder.HasKey(x => new { x.BatchId, x.StudentId });
        builder.Property(x => x.FullName).HasMaxLength(150);
        builder.Property(x => x.Gender).HasMaxLength(10);
        builder.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
    }
}
