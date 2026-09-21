using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockLens_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEfficiencyRatiosToStockFinancial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DebtorDays",
                table: "StockFinancials",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InventoryDays",
                table: "StockFinancials",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PayableDays",
                table: "StockFinancials",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DebtorDays",
                table: "StockFinancials");

            migrationBuilder.DropColumn(
                name: "InventoryDays",
                table: "StockFinancials");

            migrationBuilder.DropColumn(
                name: "PayableDays",
                table: "StockFinancials");
        }
    }
}
