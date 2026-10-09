using MealTrace.Infrastructure.Identity;
using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using MealTrace.Domain.Time;
namespace MealTrace.Infrastructure.Persistence;

public sealed partial class MealTraceDbContext(DbContextOptions<MealTraceDbContext> options, TimeProvider? clock = null)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    public DbSet<StudentImportBatch> StudentImportBatches => Set<StudentImportBatch>();
    public DbSet<ParentLinkRequest> ParentLinkRequests => Set<ParentLinkRequest>();
    public DbSet<ParentSignupOtp> ParentSignupOtps => Set<ParentSignupOtp>();
    public DbSet<AccountPasswordResetAudit> AccountPasswordResetAudits => Set<AccountPasswordResetAudit>();
    public DbSet<TeacherAssignment> TeacherAssignments => Set<TeacherAssignment>();
    public DbSet<ParentStudent> ParentStudents => Set<ParentStudent>();
    public DbSet<InspectorGrant> InspectorGrants => Set<InspectorGrant>();
    public DbSet<SchoolClass> Classes => Set<SchoolClass>();
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<MealSchedule> MealSchedules => Set<MealSchedule>();
    public DbSet<MealCalendarException> MealCalendarExceptions => Set<MealCalendarException>();
    public DbSet<MealCalendarAudit> MealCalendarAudits => Set<MealCalendarAudit>();
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
    public DbSet<SettlementDecision> SettlementDecisions => Set<SettlementDecision>();
    public DbSet<PortionAmendment> PortionAmendments => Set<PortionAmendment>();
    public DbSet<PortionAmendmentResolution> PortionAmendmentResolutions => Set<PortionAmendmentResolution>();
    public DbSet<MealEvidence> MealEvidence => Set<MealEvidence>();
    public DbSet<ReportSnapshot> ReportSnapshots => Set<ReportSnapshot>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.ApplyConfiguration(new Configurations.StudentImportBatchConfiguration());
        model.ApplyConfiguration(new Configurations.StudentImportBatchItemConfiguration());
        model.ApplyConfiguration(new Configurations.ParentLinkRequestConfiguration());
        model.Entity<ParentSignupOtp>(entity =>
        {
            entity.HasKey(x => x.PhoneNumber);
            entity.Property(x => x.PhoneNumber).HasMaxLength(10);
            entity.Property(x => x.CodeHash).HasMaxLength(64);
            entity.HasIndex(x => x.ChallengeId).IsUnique();
        });
        model.ApplyConfiguration(new Configurations.MealScheduleConfiguration());
        model.ApplyConfiguration(new Configurations.MealCalendarExceptionConfiguration());
        model.ApplyConfiguration(new Configurations.MealCalendarAuditConfiguration());
        model.ApplyConfiguration(new Configurations.MealDayConfiguration());
        model.ApplyConfiguration(new Configurations.AcademicYearConfiguration());
        model.ApplyConfiguration(new Configurations.MealAbsenceConfiguration());
        model.ApplyConfiguration(new Configurations.StudentConfiguration());
        model.ApplyConfiguration(new Configurations.EnrollmentConfiguration());
        model.ApplyConfiguration(new Configurations.AccountPasswordResetAuditConfiguration());
        model.ApplyConfiguration(new Configurations.ApplicationUserConfiguration());
        model.ApplyConfiguration(new Configurations.TeacherAssignmentConfiguration());
        model.ApplyConfiguration(new Configurations.ParentStudentConfiguration());
        model.ApplyConfiguration(new Configurations.InspectorGrantConfiguration());
        model.ApplyConfiguration(new Configurations.SchoolClassConfiguration());
        model.ApplyConfiguration(new Configurations.IngredientVersionConfiguration());
        model.ApplyConfiguration(new Configurations.RecipeVersionConfiguration());
        model.ApplyConfiguration(new Configurations.MealRegistrationConfiguration());
        model.ApplyConfiguration(new Configurations.PortionSettlementConfiguration());
        model.ApplyConfiguration(new Configurations.SettlementDecisionConfiguration());
        model.ApplyConfiguration(new Configurations.PortionAmendmentConfiguration());
        model.ApplyConfiguration(new Configurations.PortionAmendmentResolutionConfiguration());
        model.ApplyConfiguration(new Configurations.SettlementStudentConfiguration());
        model.ApplyConfiguration(new Configurations.RecipeIngredientConfiguration());
        model.ApplyConfiguration(new Configurations.ReportSnapshotConfiguration());
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
        if (ChangeTracker.Entries().Any(x => (x.State is EntityState.Modified or EntityState.Deleted) &&
            (x.Entity is StudentImportBatch or StudentImportBatchItem)))
            throw new InvalidOperationException("Import history is append-only.");
        if (ChangeTracker.Entries().Any(x => (x.State is EntityState.Modified or EntityState.Deleted) &&
            (x.Entity is PortionSettlement or SettlementStudent or SettlementDecision or PortionAmendment or PortionAmendmentStudent or PortionAmendmentResolution)))
            throw new InvalidOperationException("Settlement snapshots and amendments are append-only.");
        if (ChangeTracker.Entries<MealCalendarAudit>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Calendar audit records are append-only.");
        if (ChangeTracker.Entries<MealRegistration>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Meal exceptions are append-only; record a new event instead.");
        // Also covers seed/test creation through the DbContext, not just the HTTP endpoint.
        var added = ChangeTracker.Entries<Student>().Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToList();
        var now = _clock.GetUtcNow();
        foreach (var student in added.Where(x => x.IsActive && x.Enrollments.Count == 0))
            Enrollments.Add(new Enrollment
            {
                Student = student,
                ClassId = student.ClassId == Guid.Empty ? student.Class.Id : student.ClassId,
                StartDate = SchoolTime.Today(now),
                RecordedAt = now
            });
    }
}
