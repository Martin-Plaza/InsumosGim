using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymShop.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMercadoPagoPreferenceId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_ProviderPaymentId",
                table: "Payments");

            migrationBuilder.AddColumn<string>(
                name: "ProviderPreferenceId",
                table: "Payments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Provider_ProviderPaymentId",
                table: "Payments",
                columns: new[] { "Provider", "ProviderPaymentId" },
                filter: "[ProviderPaymentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Provider_ProviderPreferenceId",
                table: "Payments",
                columns: new[] { "Provider", "ProviderPreferenceId" },
                filter: "[ProviderPreferenceId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_Provider_ProviderPaymentId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_Provider_ProviderPreferenceId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProviderPreferenceId",
                table: "Payments");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ProviderPaymentId",
                table: "Payments",
                column: "ProviderPaymentId");
        }
    }
}
