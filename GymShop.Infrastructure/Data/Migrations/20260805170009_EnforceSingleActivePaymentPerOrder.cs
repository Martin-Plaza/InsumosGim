using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymShop.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSingleActivePaymentPerOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM [Payments]
                    WHERE [Status] IN ('Creating', 'Pending')
                    GROUP BY [OrderId]
                    HAVING COUNT(*) > 1
                )
                BEGIN
                    THROW 51000, 'Cannot enforce one active payment per order because duplicate active payments exist. Review them manually before retrying the migration.', 1;
                END;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Payments_OrderId",
                table: "Payments");

            migrationBuilder.CreateIndex(
                name: "UX_Payments_OrderId_Active",
                table: "Payments",
                column: "OrderId",
                unique: true,
                filter: "[Status] IN ('Creating', 'Pending')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Payments_OrderId_Active",
                table: "Payments");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId",
                table: "Payments",
                column: "OrderId");
        }
    }
}
