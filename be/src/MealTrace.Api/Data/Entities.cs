namespace MealTrace.Api.Data;

// Versioned factor rows and operational events are append-only.
public sealed class AcademicYear
{
    public required string Code { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}

public sealed class SchoolClass
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string SchoolYear { get; set; }
    public List<Student> Students { get; set; } = [];
}

public sealed class Student
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StudentCode { get; set; } = "HS-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
    public required string FullName { get; set; }
    // Compatibility pointer to the latest enrolled class. Date-based workflows use Enrollments.
    public Guid ClassId { get; set; }
    public SchoolClass Class { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public int Revision { get; set; }
    public List<Enrollment> Enrollments { get; set; } = [];
}

public sealed class Enrollment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public Guid ClassId { get; set; }
    public SchoolClass Class { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    // Exclusive: a child moves from A to B on D when A ends on D and B starts on D.
    public DateOnly? EndDate { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? RecordedByUserId { get; set; }
    public string Reason { get; set; } = "Ghi danh ban đầu";
    public string? EndReason { get; set; }
    public Guid? EndedByUserId { get; set; }
    public DateTimeOffset? EndRecordedAt { get; set; }
}

public sealed class Ingredient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Unit { get; set; }
    public List<IngredientVersion> Versions { get; set; } = [];
}

public sealed class IngredientVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;
    public int Version { get; set; }
    public decimal EnergyKcalPer100G { get; set; }
    public decimal ProteinGPer100G { get; set; }
    public decimal PricePerKg { get; set; }
    public decimal EdibleFraction { get; set; } = 1;
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Recipe
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public List<RecipeVersion> Versions { get; set; } = [];
}

public sealed class RecipeVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;
    public int Version { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<RecipeIngredient> Ingredients { get; set; } = [];
}

public sealed class RecipeIngredient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecipeVersionId { get; set; }
    public RecipeVersion RecipeVersion { get; set; } = null!;
    public Guid IngredientVersionId { get; set; }
    public IngredientVersion IngredientVersion { get; set; } = null!;
    public decimal GramsPerPortion { get; set; }
}

public sealed class MealDay
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly Date { get; set; }
    public string? SchoolYear { get; set; }
    public required string MealType { get; set; }
    public DateTimeOffset CutoffAt { get; set; }
    public DateTimeOffset? SettledAt { get; set; }
    public int DecisionRevision { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public List<MenuDish> Dishes { get; set; } = [];
    public List<MealRegistration> Registrations { get; set; } = [];
    public List<PortionSettlement> Settlements { get; set; } = [];
    public List<MealEvidence> Evidence { get; set; } = [];
}

public sealed class MenuDish
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MealDayId { get; set; }
    public MealDay MealDay { get; set; } = null!;
    public Guid RecipeVersionId { get; set; }
    public RecipeVersion RecipeVersion { get; set; } = null!;
}

public sealed class MealRegistration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MealDayId { get; set; }
    public MealDay MealDay { get; set; } = null!;
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    // null = restore the default decision (including any parent absence).
    public bool? WillEat { get; set; }
    public int Sequence { get; set; }
    public Guid? RecordedByUserId { get; set; }
    public string? RecordedByName { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Reason { get; set; }
    public Guid? SupersedesId { get; set; }
}

public sealed class MealAbsence
{
    public string? SchoolYear { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public Guid ReportedByUserId { get; set; }
    public ApplicationUser ReportedBy { get; set; } = null!;
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset ReportedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CancelledAt { get; set; }
}

public sealed class PortionSettlement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MealDayId { get; set; }
    public MealDay MealDay { get; set; } = null!;
    public Guid? ClassId { get; set; }
    public SchoolClass? Class { get; set; }
    public string? ClassName { get; set; }
    public int Count { get; set; }
    public DateTimeOffset CutoffAt { get; set; }
    public DateTimeOffset SettledAt { get; set; } = DateTimeOffset.UtcNow;
    public required string SettledBy { get; set; }
    public string? Reason { get; set; }
    public Guid? SupersedesId { get; set; }
    public List<SettlementStudent> Students { get; set; } = [];
}

public sealed class SettlementStudent
{
    public Guid PortionSettlementId { get; set; }
    public PortionSettlement PortionSettlement { get; set; } = null!;
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public required string StudentName { get; set; }
}

public sealed class MealEvidence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MealDayId { get; set; }
    public MealDay MealDay { get; set; } = null!;
    public required string Kind { get; set; }
    public required string Description { get; set; }
    public string? PhotoUrl { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
    public DateTimeOffset SyncedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? AmendsId { get; set; }
}

public sealed class ReportSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MealDayId { get; set; }
    public MealDay MealDay { get; set; } = null!;
    public Guid SettlementId { get; set; }
    public PortionSettlement Settlement { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SignedAt { get; set; }
    public decimal EnergyKcalPerPortion { get; set; }
    public decimal CostPerPortion { get; set; }
    public string SourceJson { get; set; } = "{}";
}
