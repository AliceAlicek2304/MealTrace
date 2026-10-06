using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealTrace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MealExceptionAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "WillEat",
                table: "MealRegistrations",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "MealRegistrations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecordedByName",
                table: "MealRegistrations",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecordedByUserId",
                table: "MealRegistrations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Sequence",
                table: "MealRegistrations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DecisionRevision",
                table: "MealDays",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Keep the legacy ordering, but never invent an actor or a supersedes link.
            migrationBuilder.Sql("""
                WITH ordered AS (
                    SELECT "Id", row_number() OVER (
                        PARTITION BY "MealDayId", "StudentId" ORDER BY "RecordedAt", "Id")::integer AS seq
                    FROM "MealRegistrations"
                )
                UPDATE "MealRegistrations" AS event SET "Sequence" = ordered.seq
                FROM ordered WHERE event."Id" = ordered."Id";
                """);
            migrationBuilder.CreateIndex(
                name: "IX_MealRegistrations_MealDayId_StudentId_Sequence",
                table: "MealRegistrations",
                columns: new[] { "MealDayId", "StudentId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MealRegistrations_RecordedByUserId",
                table: "MealRegistrations",
                column: "RecordedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_MealRegistrations_AspNetUsers_RecordedByUserId",
                table: "MealRegistrations",
                column: "RecordedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "MealRegistrations" WHERE "WillEat" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot downgrade while default-restoration events exist.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_MealRegistrations_AspNetUsers_RecordedByUserId",
                table: "MealRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_MealRegistrations_MealDayId_StudentId_Sequence",
                table: "MealRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_MealRegistrations_RecordedByUserId",
                table: "MealRegistrations");

            migrationBuilder.DropColumn(
                name: "RecordedByName",
                table: "MealRegistrations");

            migrationBuilder.DropColumn(
                name: "RecordedByUserId",
                table: "MealRegistrations");

            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "MealRegistrations");

            migrationBuilder.DropColumn(
                name: "DecisionRevision",
                table: "MealDays");

            migrationBuilder.AlterColumn<bool>(
                name: "WillEat",
                table: "MealRegistrations",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "MealRegistrations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}
