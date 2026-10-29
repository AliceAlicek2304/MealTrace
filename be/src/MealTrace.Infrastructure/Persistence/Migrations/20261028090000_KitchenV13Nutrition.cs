using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealTrace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KitchenV13Nutrition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CarbGPer100G",
                table: "IngredientVersions",
                type: "numeric(12,3)", precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EffectiveTo",
                table: "IngredientVersions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FatGPer100G",
                table: "IngredientVersions",
                type: "numeric(12,3)", precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GramsPerMl",
                table: "IngredientVersions",
                type: "numeric(8,4)", precision: 8,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecordedByUserId",
                table: "IngredientVersions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "IngredientVersions",
                type: "character varying(200)", maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceRef",
                table: "IngredientVersions",
                type: "character varying(500)", maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupersedesId",
                table: "IngredientVersions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Quantity",
                table: "RecipeIngredients",
                type: "numeric(14,3)", precision: 14,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "RecipeIngredients",
                type: "character varying(10)", maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeightBasis",
                table: "RecipeIngredients",
                type: "character varying(10)", maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "RecipeVersions",
                type: "character varying(500)", maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecordedByUserId",
                table: "RecipeVersions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RoundingKcalDecimals",
                table: "RecipeVersions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RoundingMacroDecimals",
                table: "RecipeVersions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ServingBasis",
                table: "RecipeVersions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NutrientReferenceRanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgeGroup = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Nutrient = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MinValue = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    MaxValue = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    Source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceRef = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NutrientReferenceRanges", x => x.Id);
                    table.CheckConstraint("CK_NutrientReferenceRanges_Bounds", "\"MinValue\" IS NOT NULL OR \"MaxValue\" IS NOT NULL");
                    table.CheckConstraint("CK_NutrientReferenceRanges_Interval", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" > \"EffectiveFrom\"");
                    table.CheckConstraint("CK_NutrientReferenceRanges_Nutrient", "\"Nutrient\" IN ('kcal', 'proteinG', 'fatG', 'carbG')");
                    table.CheckConstraint("CK_NutrientReferenceRanges_Order", "\"MinValue\" IS NULL OR \"MaxValue\" IS NULL OR \"MinValue\" <= \"MaxValue\"");
                    table.ForeignKey(
                        name: "FK_NutrientReferenceRanges_AspNetUsers_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IngredientVersions_RecordedByUserId",
                table: "IngredientVersions",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientVersions_SupersedesId",
                table: "IngredientVersions",
                column: "SupersedesId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeVersions_RecordedByUserId",
                table: "RecipeVersions",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NutrientReferenceRanges_RecordedByUserId",
                table: "NutrientReferenceRanges",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NutrientReferenceRanges_AgeGroup_Nutrient",
                table: "NutrientReferenceRanges",
                columns: new[] { "AgeGroup", "Nutrient" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_IngredientVersions_Interval",
                table: "IngredientVersions",
                sql: "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" > \"EffectiveFrom\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RecipeIngredients_Unit",
                table: "RecipeIngredients",
                sql: "\"Unit\" IS NULL OR \"Unit\" IN ('g', 'ml')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RecipeIngredients_WeightBasis",
                table: "RecipeIngredients",
                sql: "\"WeightBasis\" IS NULL OR \"WeightBasis\" IN ('gross', 'edible')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RecipeVersions_Rounding",
                table: "RecipeVersions",
                sql: "(\"RoundingKcalDecimals\" IS NULL OR \"RoundingKcalDecimals\" BETWEEN 0 AND 3) AND (\"RoundingMacroDecimals\" IS NULL OR \"RoundingMacroDecimals\" BETWEEN 0 AND 3)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RecipeVersions_ServingBasis",
                table: "RecipeVersions",
                sql: "\"ServingBasis\" IS NULL OR \"ServingBasis\" >= 1");

            migrationBuilder.AddForeignKey(
                name: "FK_IngredientVersions_AspNetUsers_RecordedByUserId",
                table: "IngredientVersions",
                column: "RecordedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IngredientVersions_IngredientVersions_SupersedesId",
                table: "IngredientVersions",
                column: "SupersedesId",
                principalTable: "IngredientVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeVersions_AspNetUsers_RecordedByUserId",
                table: "RecipeVersions",
                column: "RecordedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IngredientVersions_AspNetUsers_RecordedByUserId",
                table: "IngredientVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_IngredientVersions_IngredientVersions_SupersedesId",
                table: "IngredientVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_RecipeVersions_AspNetUsers_RecordedByUserId",
                table: "RecipeVersions");

            migrationBuilder.DropTable(
                name: "NutrientReferenceRanges");

            migrationBuilder.DropIndex(
                name: "IX_IngredientVersions_RecordedByUserId",
                table: "IngredientVersions");

            migrationBuilder.DropIndex(
                name: "IX_IngredientVersions_SupersedesId",
                table: "IngredientVersions");

            migrationBuilder.DropIndex(
                name: "IX_RecipeVersions_RecordedByUserId",
                table: "RecipeVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_IngredientVersions_Interval",
                table: "IngredientVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RecipeIngredients_Unit",
                table: "RecipeIngredients");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RecipeIngredients_WeightBasis",
                table: "RecipeIngredients");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RecipeVersions_Rounding",
                table: "RecipeVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RecipeVersions_ServingBasis",
                table: "RecipeVersions");

            migrationBuilder.DropColumn(
                name: "CarbGPer100G",
                table: "IngredientVersions");

            migrationBuilder.DropColumn(
                name: "EffectiveTo",
                table: "IngredientVersions");

            migrationBuilder.DropColumn(
                name: "FatGPer100G",
                table: "IngredientVersions");

            migrationBuilder.DropColumn(
                name: "GramsPerMl",
                table: "IngredientVersions");

            migrationBuilder.DropColumn(
                name: "RecordedByUserId",
                table: "IngredientVersions");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "IngredientVersions");

            migrationBuilder.DropColumn(
                name: "SourceRef",
                table: "IngredientVersions");

            migrationBuilder.DropColumn(
                name: "SupersedesId",
                table: "IngredientVersions");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "RecipeIngredients");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "RecipeIngredients");

            migrationBuilder.DropColumn(
                name: "WeightBasis",
                table: "RecipeIngredients");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "RecipeVersions");

            migrationBuilder.DropColumn(
                name: "RecordedByUserId",
                table: "RecipeVersions");

            migrationBuilder.DropColumn(
                name: "RoundingKcalDecimals",
                table: "RecipeVersions");

            migrationBuilder.DropColumn(
                name: "RoundingMacroDecimals",
                table: "RecipeVersions");

            migrationBuilder.DropColumn(
                name: "ServingBasis",
                table: "RecipeVersions");
        }
    }
}
