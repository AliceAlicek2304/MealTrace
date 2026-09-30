using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealTrace.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CoreMealWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClassId",
                table: "PortionSettlements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClassName",
                table: "PortionSettlements",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CutoffAt",
                table: "PortionSettlements",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "SchoolYear",
                table: "MealDays",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MealAbsences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    ReportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealAbsences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealAbsences_AspNetUsers_ReportedByUserId",
                        column: x => x.ReportedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MealAbsences_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SettlementStudents",
                columns: table => new
                {
                    PortionSettlementId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementStudents", x => new { x.PortionSettlementId, x.StudentId });
                    table.ForeignKey(
                        name: "FK_SettlementStudents_PortionSettlements_PortionSettlementId",
                        column: x => x.PortionSettlementId,
                        principalTable: "PortionSettlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SettlementStudents_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortionSettlements_ClassId",
                table: "PortionSettlements",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_PortionSettlements_MealDayId_ClassId",
                table: "PortionSettlements",
                columns: new[] { "MealDayId", "ClassId" },
                unique: true,
                filter: "\"ClassId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MealAbsences_ReportedByUserId",
                table: "MealAbsences",
                column: "ReportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MealAbsences_StudentId_FromDate_ToDate",
                table: "MealAbsences",
                columns: new[] { "StudentId", "FromDate", "ToDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SettlementStudents_StudentId",
                table: "SettlementStudents",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_PortionSettlements_Classes_ClassId",
                table: "PortionSettlements",
                column: "ClassId",
                principalTable: "Classes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PortionSettlements_Classes_ClassId",
                table: "PortionSettlements");

            migrationBuilder.DropTable(
                name: "MealAbsences");

            migrationBuilder.DropTable(
                name: "SettlementStudents");

            migrationBuilder.DropIndex(
                name: "IX_PortionSettlements_ClassId",
                table: "PortionSettlements");

            migrationBuilder.DropIndex(
                name: "IX_PortionSettlements_MealDayId_ClassId",
                table: "PortionSettlements");

            migrationBuilder.DropColumn(
                name: "ClassId",
                table: "PortionSettlements");

            migrationBuilder.DropColumn(
                name: "ClassName",
                table: "PortionSettlements");

            migrationBuilder.DropColumn(
                name: "CutoffAt",
                table: "PortionSettlements");

            migrationBuilder.DropColumn(
                name: "SchoolYear",
                table: "MealDays");
        }
    }
}
