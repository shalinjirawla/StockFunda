using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockLens_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateStockFinancial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CurrentPrice",
                table: "StockFinancials",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DividendYield",
                table: "StockFinancials",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OperatingProfit",
                table: "StockFinancials", 
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PbRatio",
                table: "StockFinancials",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PeRatio",
                table: "StockFinancials",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RatiosAsOfDate",
                table: "StockFinancials",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Roce",
                table: "StockFinancials",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Roe",
                table: "StockFinancials",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Week52High",
                table: "StockFinancials",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Week52Low",
                table: "StockFinancials",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentPrice",
                table: "StockFinancials");

            migrationBuilder.DropColumn(
                name: "DividendYield",
                table: "StockFinancials");

            migrationBuilder.DropColumn(
                name: "OperatingProfit",
                table: "StockFinancials");

            migrationBuilder.DropColumn(
                name: "PbRatio",
                table: "StockFinancials");

            migrationBuilder.DropColumn(
                name: "PeRatio",
                table: "StockFinancials");

            migrationBuilder.DropColumn(
                name: "RatiosAsOfDate",
                table: "StockFinancials");

            migrationBuilder.DropColumn(
                name: "Roce",
                table: "StockFinancials");

            migrationBuilder.DropColumn(
                name: "Roe",
                table: "StockFinancials");

            migrationBuilder.DropColumn(
                name: "Week52High",
                table: "StockFinancials");

            migrationBuilder.DropColumn(
                name: "Week52Low",
                table: "StockFinancials");
        }
    }
}
