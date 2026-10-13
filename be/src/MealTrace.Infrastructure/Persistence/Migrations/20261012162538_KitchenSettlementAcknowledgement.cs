using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealTrace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KitchenSettlementAcknowledgement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SettlementAcknowledgements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MealDayId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcknowledgedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AcknowledgedByName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PortionsCount = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementAcknowledgements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SettlementAcknowledgements_MealDays_MealDayId",
                        column: x => x.MealDayId,
                        principalTable: "MealDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SettlementAcknowledgements_MealDayId_AcknowledgedAt",
                table: "SettlementAcknowledgements",
                columns: new[] { "MealDayId", "AcknowledgedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SettlementAcknowledgements");
        }
    }
}
