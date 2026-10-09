using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealTrace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ParentLinkRevocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RevocationReason",
                table: "ParentLinkRequests",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RevokedAt",
                table: "ParentLinkRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RevokedByUserId",
                table: "ParentLinkRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParentLinkRequests_RevokedByUserId",
                table: "ParentLinkRequests",
                column: "RevokedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ParentLinkRequests_AspNetUsers_RevokedByUserId",
                table: "ParentLinkRequests",
                column: "RevokedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ParentLinkRequests_AspNetUsers_RevokedByUserId",
                table: "ParentLinkRequests");

            migrationBuilder.DropIndex(
                name: "IX_ParentLinkRequests_RevokedByUserId",
                table: "ParentLinkRequests");

            migrationBuilder.DropColumn(
                name: "RevocationReason",
                table: "ParentLinkRequests");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "ParentLinkRequests");

            migrationBuilder.DropColumn(
                name: "RevokedByUserId",
                table: "ParentLinkRequests");
        }
    }
}
