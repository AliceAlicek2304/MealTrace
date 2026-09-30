using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealTrace.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AccountPasswordResetAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountPasswordResetAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PerformedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountPasswordResetAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountPasswordResetAudits_AspNetUsers_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountPasswordResetAudits_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountPasswordResetAudits_PerformedByUserId",
                table: "AccountPasswordResetAudits",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountPasswordResetAudits_UserId_PerformedAt",
                table: "AccountPasswordResetAudits",
                columns: new[] { "UserId", "PerformedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountPasswordResetAudits");
        }
    }
}
