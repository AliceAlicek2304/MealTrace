using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealTrace.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Classes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SchoolYear = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Classes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ingredients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ingredients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MealDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    MealType = table.Column<string>(type: "text", nullable: false),
                    CutoffAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealDays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Recipes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Students_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngredientVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    EnergyKcalPer100G = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    ProteinGPer100G = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    PricePerKg = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    EdibleFraction = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientVersions_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MealEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MealDayId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    PhotoUrl = table.Column<string>(type: "text", nullable: true),
                    CapturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AmendsId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealEvidence_MealDays_MealDayId",
                        column: x => x.MealDayId,
                        principalTable: "MealDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PortionSettlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MealDayId = table.Column<Guid>(type: "uuid", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    SettledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SettledBy = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    SupersedesId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortionSettlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortionSettlements_MealDays_MealDayId",
                        column: x => x.MealDayId,
                        principalTable: "MealDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeVersions_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MealRegistrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MealDayId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    WillEat = table.Column<bool>(type: "boolean", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    SupersedesId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealRegistrations_MealDays_MealDayId",
                        column: x => x.MealDayId,
                        principalTable: "MealDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MealRegistrations_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReportSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MealDayId = table.Column<Guid>(type: "uuid", nullable: false),
                    SettlementId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EnergyKcalPerPortion = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    CostPerPortion = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    SourceJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportSnapshots_MealDays_MealDayId",
                        column: x => x.MealDayId,
                        principalTable: "MealDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReportSnapshots_PortionSettlements_SettlementId",
                        column: x => x.SettlementId,
                        principalTable: "PortionSettlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MenuDishes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MealDayId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeVersionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuDishes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuDishes_MealDays_MealDayId",
                        column: x => x.MealDayId,
                        principalTable: "MealDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuDishes_RecipeVersions_RecipeVersionId",
                        column: x => x.RecipeVersionId,
                        principalTable: "RecipeVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeIngredients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    GramsPerPortion = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeIngredients_IngredientVersions_IngredientVersionId",
                        column: x => x.IngredientVersionId,
                        principalTable: "IngredientVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecipeIngredients_RecipeVersions_RecipeVersionId",
                        column: x => x.RecipeVersionId,
                        principalTable: "RecipeVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Classes_SchoolYear_Name",
                table: "Classes",
                columns: new[] { "SchoolYear", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientVersions_IngredientId_Version",
                table: "IngredientVersions",
                columns: new[] { "IngredientId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MealDays_Date_MealType",
                table: "MealDays",
                columns: new[] { "Date", "MealType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MealEvidence_MealDayId",
                table: "MealEvidence",
                column: "MealDayId");

            migrationBuilder.CreateIndex(
                name: "IX_MealRegistrations_MealDayId_StudentId_RecordedAt",
                table: "MealRegistrations",
                columns: new[] { "MealDayId", "StudentId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MealRegistrations_StudentId",
                table: "MealRegistrations",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuDishes_MealDayId",
                table: "MenuDishes",
                column: "MealDayId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuDishes_RecipeVersionId",
                table: "MenuDishes",
                column: "RecipeVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_PortionSettlements_MealDayId_SettledAt",
                table: "PortionSettlements",
                columns: new[] { "MealDayId", "SettledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredients_IngredientVersionId",
                table: "RecipeIngredients",
                column: "IngredientVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredients_RecipeVersionId",
                table: "RecipeIngredients",
                column: "RecipeVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeVersions_RecipeId_Version",
                table: "RecipeVersions",
                columns: new[] { "RecipeId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportSnapshots_MealDayId",
                table: "ReportSnapshots",
                column: "MealDayId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSnapshots_SettlementId",
                table: "ReportSnapshots",
                column: "SettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_ClassId",
                table: "Students",
                column: "ClassId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MealEvidence");

            migrationBuilder.DropTable(
                name: "MealRegistrations");

            migrationBuilder.DropTable(
                name: "MenuDishes");

            migrationBuilder.DropTable(
                name: "RecipeIngredients");

            migrationBuilder.DropTable(
                name: "ReportSnapshots");

            migrationBuilder.DropTable(
                name: "Students");

            migrationBuilder.DropTable(
                name: "IngredientVersions");

            migrationBuilder.DropTable(
                name: "RecipeVersions");

            migrationBuilder.DropTable(
                name: "PortionSettlements");

            migrationBuilder.DropTable(
                name: "Classes");

            migrationBuilder.DropTable(
                name: "Ingredients");

            migrationBuilder.DropTable(
                name: "Recipes");

            migrationBuilder.DropTable(
                name: "MealDays");
        }
    }
}
