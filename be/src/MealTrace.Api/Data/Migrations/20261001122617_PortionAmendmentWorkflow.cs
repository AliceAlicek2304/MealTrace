using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealTrace.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PortionAmendmentWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PortionSettlements_MealDayId_ClassId",
                table: "PortionSettlements");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "PortionSettlements",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "PortionAmendments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseSettlementId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentName = table.Column<string>(type: "text", nullable: false),
                    StudentCode = table.Column<string>(type: "text", nullable: false),
                    EnrollmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    WasEating = table.Column<bool>(type: "boolean", nullable: false),
                    WillEat = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByName = table.Column<string>(type: "text", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortionAmendments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortionAmendments_PortionSettlements_BaseSettlementId",
                        column: x => x.BaseSettlementId,
                        principalTable: "PortionSettlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SettlementDecisions",
                columns: table => new
                {
                    PortionSettlementId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentName = table.Column<string>(type: "text", nullable: false),
                    StudentCode = table.Column<string>(type: "text", nullable: false),
                    WillEat = table.Column<bool>(type: "boolean", nullable: false),
                    EnrollmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    AbsenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExceptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    AmendmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementDecisions", x => new { x.PortionSettlementId, x.StudentId });
                    table.ForeignKey(
                        name: "FK_SettlementDecisions_PortionSettlements_PortionSettlementId",
                        column: x => x.PortionSettlementId,
                        principalTable: "PortionSettlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PortionAmendmentResolutions",
                columns: table => new
                {
                    AmendmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Approved = table.Column<bool>(type: "boolean", nullable: false),
                    AppliedSettlementId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedByName = table.Column<string>(type: "text", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortionAmendmentResolutions", x => x.AmendmentId);
                    table.ForeignKey(
                        name: "FK_PortionAmendmentResolutions_PortionAmendments_AmendmentId",
                        column: x => x.AmendmentId,
                        principalTable: "PortionAmendments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PortionAmendmentResolutions_PortionSettlements_AppliedSettl~",
                        column: x => x.AppliedSettlementId,
                        principalTable: "PortionSettlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortionSettlements_MealDayId_ClassId_Version",
                table: "PortionSettlements",
                columns: new[] { "MealDayId", "ClassId", "Version" },
                unique: true,
                filter: "\"ClassId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PortionSettlements_SupersedesId",
                table: "PortionSettlements",
                column: "SupersedesId");

            migrationBuilder.CreateIndex(
                name: "IX_PortionAmendmentResolutions_AppliedSettlementId",
                table: "PortionAmendmentResolutions",
                column: "AppliedSettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_PortionAmendments_BaseSettlementId_RequestedAt",
                table: "PortionAmendments",
                columns: new[] { "BaseSettlementId", "RequestedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_PortionSettlements_PortionSettlements_SupersedesId",
                table: "PortionSettlements",
                column: "SupersedesId",
                principalTable: "PortionSettlements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PortionSettlements_PortionSettlements_SupersedesId",
                table: "PortionSettlements");

            migrationBuilder.DropTable(
                name: "PortionAmendmentResolutions");

            migrationBuilder.DropTable(
                name: "SettlementDecisions");

            migrationBuilder.DropTable(
                name: "PortionAmendments");

            migrationBuilder.DropIndex(
                name: "IX_PortionSettlements_MealDayId_ClassId_Version",
                table: "PortionSettlements");

            migrationBuilder.DropIndex(
                name: "IX_PortionSettlements_SupersedesId",
                table: "PortionSettlements");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "PortionSettlements");

            migrationBuilder.CreateIndex(
                name: "IX_PortionSettlements_MealDayId_ClassId",
                table: "PortionSettlements",
                columns: new[] { "MealDayId", "ClassId" },
                unique: true,
                filter: "\"ClassId\" IS NOT NULL");
        }
    }
}
