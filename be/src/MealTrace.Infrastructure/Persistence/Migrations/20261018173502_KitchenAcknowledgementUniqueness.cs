using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealTrace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KitchenAcknowledgementUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SettlementAcknowledgements_MealDayId_AcknowledgedBy_Snapsho~",
                table: "SettlementAcknowledgements",
                columns: new[] { "MealDayId", "AcknowledgedBy", "SnapshotToken" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SettlementAcknowledgements_MealDayId_AcknowledgedBy_Snapsho~",
                table: "SettlementAcknowledgements");
        }
    }
}
