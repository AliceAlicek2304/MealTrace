using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealTrace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KitchenAcknowledgedSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SnapshotToken",
                table: "SettlementAcknowledgements",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SnapshotToken",
                table: "SettlementAcknowledgements");
        }
    }
}
