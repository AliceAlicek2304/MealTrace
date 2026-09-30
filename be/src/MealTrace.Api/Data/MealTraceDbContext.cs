using Microsoft.EntityFrameworkCore;

namespace MealTrace.Api.Data;

public sealed class MealTraceDbContext(DbContextOptions<MealTraceDbContext> options) : DbContext(options)
{
    public DbSet<SchoolClass> Classes => Set<SchoolClass>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<IngredientVersion> IngredientVersions => Set<IngredientVersion>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeVersion> RecipeVersions => Set<RecipeVersion>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<MealDay> MealDays => Set<MealDay>();
    public DbSet<MenuDish> MenuDishes => Set<MenuDish>();
    public DbSet<MealRegistration> MealRegistrations => Set<MealRegistration>();
    public DbSet<PortionSettlement> PortionSettlements => Set<PortionSettlement>();
    public DbSet<MealEvidence> MealEvidence => Set<MealEvidence>();
    public DbSet<ReportSnapshot> ReportSnapshots => Set<ReportSnapshot>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<SchoolClass>().HasIndex(x => new { x.SchoolYear, x.Name }).IsUnique();
        model.Entity<MealDay>().HasIndex(x => new { x.Date, x.MealType }).IsUnique();
        model.Entity<IngredientVersion>().HasIndex(x => new { x.IngredientId, x.Version }).IsUnique();
        model.Entity<RecipeVersion>().HasIndex(x => new { x.RecipeId, x.Version }).IsUnique();
        model.Entity<MealRegistration>().HasIndex(x => new { x.MealDayId, x.StudentId, x.RecordedAt });
        model.Entity<PortionSettlement>().HasIndex(x => new { x.MealDayId, x.SettledAt });
        model.Entity<IngredientVersion>().Property(x => x.EnergyKcalPer100G).HasPrecision(12, 3);
        model.Entity<IngredientVersion>().Property(x => x.ProteinGPer100G).HasPrecision(12, 3);
        model.Entity<IngredientVersion>().Property(x => x.PricePerKg).HasPrecision(14, 2);
        model.Entity<IngredientVersion>().Property(x => x.EdibleFraction).HasPrecision(5, 4);
        model.Entity<RecipeIngredient>().Property(x => x.GramsPerPortion).HasPrecision(12, 3);
        model.Entity<ReportSnapshot>().Property(x => x.EnergyKcalPerPortion).HasPrecision(12, 3);
        model.Entity<ReportSnapshot>().Property(x => x.CostPerPortion).HasPrecision(14, 2);
        model.Entity<ReportSnapshot>().Property(x => x.SourceJson).HasColumnType("jsonb");
    }
}
