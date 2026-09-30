using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealTrace.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RetireNutritionAndAccountingRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Revoke old tokens and suspend accounts that lose their only access.
            // Keep accounts/history and retain any remaining supported role or inspector grant.
            migrationBuilder.Sql("""
                UPDATE "AspNetUsers" AS u
                SET "SecurityStamp" = md5(random()::text || clock_timestamp()::text),
                    "IsActive" = CASE WHEN
                        EXISTS (SELECT 1 FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
                            WHERE ur."UserId" = u."Id" AND r."NormalizedName" IN ('ADMIN','TEACHER','KITCHEN_STAFF','PARENT'))
                        OR EXISTS (SELECT 1 FROM "InspectorGrants" g WHERE g."UserId" = u."Id" AND g."ExpiresOn" >= (CURRENT_TIMESTAMP AT TIME ZONE 'Asia/Ho_Chi_Minh')::date)
                        THEN u."IsActive" ELSE FALSE END
                WHERE EXISTS (SELECT 1 FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
                    WHERE ur."UserId" = u."Id" AND r."NormalizedName" IN ('NUTRITIONIST','ACCOUNTANT'));

                DELETE FROM "AspNetUserRoles" WHERE "RoleId" IN
                    (SELECT "Id" FROM "AspNetRoles" WHERE "NormalizedName" IN ('NUTRITIONIST','ACCOUNTANT'));
                DELETE FROM "AspNetRoles" WHERE "NormalizedName" IN ('NUTRITIONIST','ACCOUNTANT');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore role definitions only; never silently regrant removed access.
            migrationBuilder.Sql("""
                INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
                SELECT md5(random()::text || clock_timestamp()::text)::uuid, name, name, md5(random()::text)
                FROM (VALUES ('NUTRITIONIST'), ('ACCOUNTANT')) AS roles(name)
                WHERE NOT EXISTS (SELECT 1 FROM "AspNetRoles" r WHERE r."NormalizedName" = roles.name);
                """);
        }
    }
}
