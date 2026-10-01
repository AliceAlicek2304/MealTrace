using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealTrace.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MealOperatingCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "MealDays",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                table: "MealDays",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "MealCalendarAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorName = table.Column<string>(type: "text", nullable: false),
                    SchoolYear = table.Column<string>(type: "text", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: true),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    BeforeJson = table.Column<string>(type: "text", nullable: false),
                    AfterJson = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealCalendarAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MealCalendarExceptions",
                columns: table => new
                {
                    SchoolYear = table.Column<string>(type: "character varying(30)", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    IsOpen = table.Column<bool>(type: "boolean", nullable: false),
                    MealTypesJson = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealCalendarExceptions", x => new { x.SchoolYear, x.Date });
                    table.ForeignKey(
                        name: "FK_MealCalendarExceptions_AcademicYears_SchoolYear",
                        column: x => x.SchoolYear,
                        principalTable: "AcademicYears",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MealSchedules",
                columns: table => new
                {
                    SchoolYear = table.Column<string>(type: "character varying(30)", nullable: false),
                    WeekdayMask = table.Column<int>(type: "integer", nullable: false),
                    MealTypesJson = table.Column<string>(type: "text", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealSchedules", x => x.SchoolYear);
                    table.ForeignKey(
                        name: "FK_MealSchedules_AcademicYears_SchoolYear",
                        column: x => x.SchoolYear,
                        principalTable: "AcademicYears",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MealCalendarAudits_SchoolYear_RecordedAt",
                table: "MealCalendarAudits",
                columns: new[] { "SchoolYear", "RecordedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MealCalendarAudits");

            migrationBuilder.DropTable(
                name: "MealCalendarExceptions");

            migrationBuilder.DropTable(
                name: "MealSchedules");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "MealDays");

            migrationBuilder.DropColumn(
                name: "IsCancelled",
                table: "MealDays");
        }
    }
}
