using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace MealTrace.Api.Data;

public sealed class MealTraceDbContext(DbContextOptions<MealTraceDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<AccountPasswordResetAudit> AccountPasswordResetAudits => Set<AccountPasswordResetAudit>();
    public DbSet<TeacherAssignment> TeacherAssignments => Set<TeacherAssignment>();
    public DbSet<ParentStudent> ParentStudents => Set<ParentStudent>();
    public DbSet<InspectorGrant> InspectorGrants => Set<InspectorGrant>();
    public DbSet<SchoolClass> Classes => Set<SchoolClass>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<IngredientVersion> IngredientVersions => Set<IngredientVersion>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeVersion> RecipeVersions => Set<RecipeVersion>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<MealDay> MealDays => Set<MealDay>();
    public DbSet<MenuDish> MenuDishes => Set<MenuDish>();
    public DbSet<MealRegistration> MealRegistrations => Set<MealRegistration>();
    public DbSet<MealAbsence> MealAbsences => Set<MealAbsence>();
    public DbSet<PortionSettlement> PortionSettlements => Set<PortionSettlement>();
    public DbSet<SettlementStudent> SettlementStudents => Set<SettlementStudent>();
    public DbSet<MealEvidence> MealEvidence => Set<MealEvidence>();
    public DbSet<ReportSnapshot> ReportSnapshots => Set<ReportSnapshot>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.Entity<Student>().Property(x => x.StudentCode).HasMaxLength(40);
        model.Entity<Student>().HasIndex(x => x.StudentCode).IsUnique();
        model.Entity<Student>().Property(x => x.Revision).IsConcurrencyToken();
        model.Entity<Enrollment>().HasIndex(x => new { x.StudentId, x.StartDate }).IsUnique();
        model.Entity<Enrollment>().HasIndex(x => new { x.ClassId, x.StartDate, x.EndDate });
        model.Entity<Enrollment>().HasIndex(x => x.StudentId).IsUnique().HasFilter("\"EndDate\" IS NULL");
        model.Entity<Enrollment>().ToTable(t => t.HasCheckConstraint("CK_Enrollment_Dates", "\"EndDate\" IS NULL OR \"EndDate\" > \"StartDate\""));
        model.Entity<Enrollment>().Property(x => x.Reason).HasMaxLength(500);
        model.Entity<Enrollment>().Property(x => x.EndReason).HasMaxLength(500);
        model.Entity<Enrollment>().HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Enrollment>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Enrollment>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.EndedByUserId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<AccountPasswordResetAudit>().Property(x => x.Reason).HasMaxLength(500);
        model.Entity<AccountPasswordResetAudit>().HasIndex(x => new { x.UserId, x.PerformedAt });
        model.Entity<AccountPasswordResetAudit>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<AccountPasswordResetAudit>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.PerformedByUserId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<ApplicationUser>().HasIndex(x => x.PhoneNumber).IsUnique();
        model.Entity<ApplicationUser>().HasIndex(x => x.NormalizedEmail).IsUnique();
        model.Entity<TeacherAssignment>().HasKey(x => new { x.UserId, x.ClassId });
        model.Entity<ParentStudent>().HasKey(x => new { x.UserId, x.StudentId });
        model.Entity<InspectorGrant>().HasKey(x => x.UserId);
        model.Entity<InspectorGrant>().HasOne(x => x.User).WithOne(x => x.InspectorGrant).HasForeignKey<InspectorGrant>(x => x.UserId);
        model.Entity<InspectorGrant>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.GrantedById).OnDelete(DeleteBehavior.Restrict);
        model.Entity<SchoolClass>().HasIndex(x => new { x.SchoolYear, x.Name }).IsUnique();
        model.Entity<MealDay>().HasIndex(x => new { x.Date, x.MealType }).IsUnique();
        model.Entity<IngredientVersion>().HasIndex(x => new { x.IngredientId, x.Version }).IsUnique();
        model.Entity<RecipeVersion>().HasIndex(x => new { x.RecipeId, x.Version }).IsUnique();
        model.Entity<MealRegistration>().HasIndex(x => new { x.MealDayId, x.StudentId, x.RecordedAt });
        model.Entity<MealAbsence>().HasIndex(x => new { x.StudentId, x.FromDate, x.ToDate });
        model.Entity<MealAbsence>().HasOne(x => x.ReportedBy).WithMany().HasForeignKey(x => x.ReportedByUserId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<PortionSettlement>().HasIndex(x => new { x.MealDayId, x.SettledAt });
        model.Entity<PortionSettlement>().HasIndex(x => new { x.MealDayId, x.ClassId }).IsUnique().HasFilter("\"ClassId\" IS NOT NULL");
        model.Entity<SettlementStudent>().HasKey(x => new { x.PortionSettlementId, x.StudentId });
        model.Entity<SettlementStudent>().HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<IngredientVersion>().Property(x => x.EnergyKcalPer100G).HasPrecision(12, 3);
        model.Entity<IngredientVersion>().Property(x => x.ProteinGPer100G).HasPrecision(12, 3);
        model.Entity<IngredientVersion>().Property(x => x.PricePerKg).HasPrecision(14, 2);
        model.Entity<IngredientVersion>().Property(x => x.EdibleFraction).HasPrecision(5, 4);
        model.Entity<RecipeIngredient>().Property(x => x.GramsPerPortion).HasPrecision(12, 3);
        model.Entity<ReportSnapshot>().Property(x => x.EnergyKcalPerPortion).HasPrecision(12, 3);
        model.Entity<ReportSnapshot>().Property(x => x.CostPerPortion).HasPrecision(14, 2);
        model.Entity<ReportSnapshot>().Property(x => x.SourceJson).HasColumnType("jsonb");
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareEnrollments();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PrepareEnrollments();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void PrepareEnrollments()
    {
        // Also covers seed/test creation through the DbContext, not just the HTTP endpoint.
        var added = ChangeTracker.Entries<Student>().Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToList();
        foreach (var student in added.Where(x => x.IsActive && x.Enrollments.Count == 0))
            Enrollments.Add(new Enrollment { Student = student, ClassId = student.ClassId == Guid.Empty ? student.Class.Id : student.ClassId,
                StartDate = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime) });
    }
}
