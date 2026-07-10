using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymShop.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSinglePendingOrderPerUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_UserId",
                table: "Orders");


            migrationBuilder.Sql(@"
DECLARE @DuplicatePendingOrders TABLE (Id int NOT NULL PRIMARY KEY);

INSERT INTO @DuplicatePendingOrders (Id)
SELECT Id
FROM (
    SELECT
        Id,
        ROW_NUMBER() OVER (PARTITION BY UserId ORDER BY CreatedAt DESC, Id DESC) AS PendingRank
    FROM Orders
    WHERE Status = 'Pending'
) pending
WHERE PendingRank > 1;

UPDATE p
SET Stock = p.Stock + restored.Quantity
FROM Products p
INNER JOIN (
    SELECT oi.ProductId, SUM(oi.Quantity) AS Quantity
    FROM OrderItems oi
    INNER JOIN @DuplicatePendingOrders d ON d.Id = oi.OrderId
    GROUP BY oi.ProductId
) restored ON restored.ProductId = p.Id;

UPDATE Orders
SET Status = 'Canceled', UpdatedAt = SYSUTCDATETIME()
WHERE Id IN (SELECT Id FROM @DuplicatePendingOrders);
");
            migrationBuilder.CreateIndex(
                name: "UX_Orders_UserId_Pending",
                table: "Orders",
                column: "UserId",
                unique: true,
                filter: "[Status] = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Orders_UserId_Pending",
                table: "Orders");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId",
                table: "Orders",
                column: "UserId");
        }
    }
}



