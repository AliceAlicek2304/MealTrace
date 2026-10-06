using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealTrace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BatchPortionAmendments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KitchenAdjustment",
                table: "PortionSettlements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsQuantityOnly",
                table: "PortionAmendments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "PortionAmendments",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "PortionAmendmentStudents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentName = table.Column<string>(type: "text", nullable: false),
                    StudentCode = table.Column<string>(type: "text", nullable: false),
                    EnrollmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    PortionAmendmentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortionAmendmentStudents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortionAmendmentStudents_PortionAmendments_PortionAmendment~",
                        column: x => x.PortionAmendmentId,
                        principalTable: "PortionAmendments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortionAmendmentStudents_PortionAmendmentId_StudentId",
                table: "PortionAmendmentStudents",
                columns: new[] { "PortionAmendmentId", "StudentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortionAmendmentStudents");

            migrationBuilder.DropColumn(
                name: "KitchenAdjustment",
                table: "PortionSettlements");

            migrationBuilder.DropColumn(
                name: "IsQuantityOnly",
                table: "PortionAmendments");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "PortionAmendments");
        }
    }
}
