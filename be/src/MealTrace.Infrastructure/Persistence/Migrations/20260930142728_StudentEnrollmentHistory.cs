using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealTrace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StudentEnrollmentHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "Students",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StudentCode",
                table: "Students",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Enrollments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EndReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EndedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EndRecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enrollments", x => x.Id);
                    table.CheckConstraint("CK_Enrollment_Dates", "\"EndDate\" IS NULL OR \"EndDate\" > \"StartDate\"");
                    table.ForeignKey(
                        name: "FK_Enrollments_AspNetUsers_EndedByUserId",
                        column: x => x.EndedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Enrollments_AspNetUsers_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Enrollments_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Enrollments_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Legacy data has no actual enrollment dates. Start at migration day and mark the provenance.
            // Existing settlement snapshots remain the source of truth for historical meal days.
            migrationBuilder.Sql("""
                UPDATE "Students" SET "StudentCode" = 'HS-' || upper(replace("Id"::text, '-', ''));
                INSERT INTO "Enrollments" ("Id", "StudentId", "ClassId", "StartDate", "RecordedAt", "Reason")
                SELECT "Id", "Id", "ClassId", (CURRENT_TIMESTAMP AT TIME ZONE 'Asia/Ho_Chi_Minh')::date,
                    CURRENT_TIMESTAMP, 'Chuyển đổi dữ liệu cũ: ngày bắt đầu là ngày migration, cần đối chiếu hồ sơ'
                FROM "Students" WHERE "IsActive" = true;
                """);
            migrationBuilder.CreateIndex(
                name: "IX_Students_StudentCode",
                table: "Students",
                column: "StudentCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_ClassId_StartDate_EndDate",
                table: "Enrollments",
                columns: new[] { "ClassId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_EndedByUserId",
                table: "Enrollments",
                column: "EndedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_RecordedByUserId",
                table: "Enrollments",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_StudentId",
                table: "Enrollments",
                column: "StudentId",
                unique: true,
                filter: "\"EndDate\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_StudentId_StartDate",
                table: "Enrollments",
                columns: new[] { "StudentId", "StartDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Enrollments");

            migrationBuilder.DropIndex(
                name: "IX_Students_StudentCode",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "StudentCode",
                table: "Students");
        }
    }
}
