using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealTrace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KitchenSourcesMenusEstimates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Suppliers_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Contact = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PriorVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierRevisions", x => x.Id);
                    table.CheckConstraint("CK_SupplierRevisions_Version", "\"Version\" >= 1");
                    table.ForeignKey(
                        name: "FK_SupplierRevisions_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierRevisions_SupplierRevisions_PriorVersionId",
                        column: x => x.PriorVersionId,
                        principalTable: "SupplierRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierRevisions_AspNetUsers_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IngredientSourceEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientSourceEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientSourceEntries_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientSourceEntries_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IngredientSourceEntryVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    SupplierRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DeliveryRef = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LotRef = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PriorVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientSourceEntryVersions", x => x.Id);
                    table.CheckConstraint("CK_IngredientSourceEntryVersions_Quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_IngredientSourceEntryVersions_Unit", "\"Unit\" IN ('g', 'ml')");
                    table.CheckConstraint("CK_IngredientSourceEntryVersions_Version", "\"Version\" >= 1");
                    table.ForeignKey(
                        name: "FK_IngredientSourceEntryVersions_IngredientSourceEntries_SourceEntryId",
                        column: x => x.SourceEntryId,
                        principalTable: "IngredientSourceEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientSourceEntryVersions_SupplierRevisions_SupplierRevisionId",
                        column: x => x.SupplierRevisionId,
                        principalTable: "SupplierRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientSourceEntryVersions_IngredientSourceEntryVersions_PriorVersionId",
                        column: x => x.PriorVersionId,
                        principalTable: "IngredientSourceEntryVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientSourceEntryVersions_AspNetUsers_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Menus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MealDayId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Menus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Menus_MealDays_MealDayId",
                        column: x => x.MealDayId,
                        principalTable: "MealDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Menus_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MenuVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PriorVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuVersions", x => x.Id);
                    table.CheckConstraint("CK_MenuVersions_Version", "\"Version\" >= 1");
                    table.ForeignKey(
                        name: "FK_MenuVersions_Menus_MenuId",
                        column: x => x.MenuId,
                        principalTable: "Menus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MenuVersions_MenuVersions_PriorVersionId",
                        column: x => x.PriorVersionId,
                        principalTable: "MenuVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MenuVersions_AspNetUsers_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MenuItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlannedPortions = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItems", x => x.Id);
                    table.CheckConstraint("CK_MenuItems_PlannedPortions", "\"PlannedPortions\" IS NULL OR \"PlannedPortions\" >= 1");
                    table.ForeignKey(
                        name: "FK_MenuItems_MenuVersions_MenuVersionId",
                        column: x => x.MenuVersionId,
                        principalTable: "MenuVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MenuItems_RecipeVersions_RecipeVersionId",
                        column: x => x.RecipeVersionId,
                        principalTable: "RecipeVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MenuTransitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuTransitions", x => x.Id);
                    table.CheckConstraint("CK_MenuTransitions_Sequence", "\"Sequence\" >= 1");
                    table.CheckConstraint("CK_MenuTransitions_State", "\"State\" IN ('draft', 'submitted', 'approved', 'rejected', 'published')");
                    table.ForeignKey(
                        name: "FK_MenuTransitions_MenuVersions_MenuVersionId",
                        column: x => x.MenuVersionId,
                        principalTable: "MenuVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MenuTransitions_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MenuNutritionSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AgeGroup = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AgeComparisonStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuNutritionSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuNutritionSnapshots_MenuVersions_MenuVersionId",
                        column: x => x.MenuVersionId,
                        principalTable: "MenuVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MenuNutritionSnapshots_AspNetUsers_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IngredientEstimates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InputJson = table.Column<string>(type: "text", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientEstimates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientEstimates_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_CreatedByUserId",
                table: "Suppliers",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierRevisions_PriorVersionId",
                table: "SupplierRevisions",
                column: "PriorVersionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierRevisions_RecordedByUserId",
                table: "SupplierRevisions",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierRevisions_SupplierId_Version",
                table: "SupplierRevisions",
                columns: new[] { "SupplierId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSourceEntries_CreatedByUserId",
                table: "IngredientSourceEntries",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSourceEntries_IngredientId",
                table: "IngredientSourceEntries",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSourceEntryVersions_PriorVersionId",
                table: "IngredientSourceEntryVersions",
                column: "PriorVersionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSourceEntryVersions_RecordedByUserId",
                table: "IngredientSourceEntryVersions",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSourceEntryVersions_ReferenceDate",
                table: "IngredientSourceEntryVersions",
                column: "ReferenceDate");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSourceEntryVersions_SourceEntryId_Version",
                table: "IngredientSourceEntryVersions",
                columns: new[] { "SourceEntryId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientSourceEntryVersions_SupplierRevisionId",
                table: "IngredientSourceEntryVersions",
                column: "SupplierRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_Menus_CreatedByUserId",
                table: "Menus",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Menus_MealDayId",
                table: "Menus",
                column: "MealDayId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuVersions_MenuId_Version",
                table: "MenuVersions",
                columns: new[] { "MenuId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuVersions_PriorVersionId",
                table: "MenuVersions",
                column: "PriorVersionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuVersions_RecordedByUserId",
                table: "MenuVersions",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_MenuVersionId_RecipeVersionId",
                table: "MenuItems",
                columns: new[] { "MenuVersionId", "RecipeVersionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_RecipeVersionId",
                table: "MenuItems",
                column: "RecipeVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuTransitions_ActorUserId",
                table: "MenuTransitions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuTransitions_MenuVersionId_Sequence",
                table: "MenuTransitions",
                columns: new[] { "MenuVersionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuNutritionSnapshots_MenuVersionId",
                table: "MenuNutritionSnapshots",
                column: "MenuVersionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuNutritionSnapshots_RecordedByUserId",
                table: "MenuNutritionSnapshots",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientEstimates_CreatedByUserId",
                table: "IngredientEstimates",
                column: "CreatedByUserId");

            migrationBuilder.AddColumn<string>(
                name: "Basis",
                table: "NutrientReferenceRanges",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MealType",
                table: "NutrientReferenceRanges",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "NutrientReferenceRanges",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupersedesId",
                table: "NutrientReferenceRanges",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "NutrientReferenceRanges",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_NutrientReferenceRanges_SupersedesId",
                table: "NutrientReferenceRanges",
                column: "SupersedesId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_NutrientReferenceRanges_Basis",
                table: "NutrientReferenceRanges",
                sql: "\"Basis\" IS NULL OR \"Basis\" IN ('perMeal', 'daily')");

            migrationBuilder.AddForeignKey(
                name: "FK_NutrientReferenceRanges_NutrientReferenceRanges_SupersedesId",
                table: "NutrientReferenceRanges",
                column: "SupersedesId",
                principalTable: "NutrientReferenceRanges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NutrientReferenceRanges_NutrientReferenceRanges_SupersedesId",
                table: "NutrientReferenceRanges");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NutrientReferenceRanges_Basis",
                table: "NutrientReferenceRanges");

            migrationBuilder.DropIndex(
                name: "IX_NutrientReferenceRanges_SupersedesId",
                table: "NutrientReferenceRanges");

            migrationBuilder.DropColumn(
                name: "Basis",
                table: "NutrientReferenceRanges");

            migrationBuilder.DropColumn(
                name: "MealType",
                table: "NutrientReferenceRanges");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "NutrientReferenceRanges");

            migrationBuilder.DropColumn(
                name: "SupersedesId",
                table: "NutrientReferenceRanges");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "NutrientReferenceRanges");

            migrationBuilder.DropTable(
                name: "IngredientEstimates");

            migrationBuilder.DropTable(
                name: "MenuNutritionSnapshots");

            migrationBuilder.DropTable(
                name: "MenuTransitions");

            migrationBuilder.DropTable(
                name: "MenuItems");

            migrationBuilder.DropTable(
                name: "MenuVersions");

            migrationBuilder.DropTable(
                name: "Menus");

            migrationBuilder.DropTable(
                name: "IngredientSourceEntryVersions");

            migrationBuilder.DropTable(
                name: "IngredientSourceEntries");

            migrationBuilder.DropTable(
                name: "SupplierRevisions");

            migrationBuilder.DropTable(
                name: "Suppliers");
        }
    }
}
