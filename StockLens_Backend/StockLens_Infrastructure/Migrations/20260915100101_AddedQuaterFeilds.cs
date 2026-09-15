using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockLens_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedQuaterFeilds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Depreciation",
                table: "StockFinancials",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Expenses",
                table: "StockFinancials",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Interest",
                table: "StockFinancials",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OperatingProfitMargin",
                table: "StockFinancials",
                type: "decimal(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OtherIncome",
                table: "StockFinancials",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProfitBeforeTax",
                table: "StockFinancials",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Tax",
                table: "StockFinancials",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxPercentage",
                table: "StockFinancials",
                type: "decimal(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Depreciation",
                table: "StockFinancials");

            migrationBuilder.DropColumn(
                name: "Expenses",
                table: "StockFinancials");

            migrationBuilder.DropColumn(
                name: "Interest",
                table: "StockFinancials");

            migrationBuilder.DropColumn(
                name: "OperatingProfitMargin",
                table: "StockFinancials");

            migrationBuilder.DropColumn(
                name: "OtherIncome",
                table: "StockFinancials");

            migrationBuilder.DropColumn(
                name: "ProfitBeforeTax",
                table: "StockFinancials");

            migrationBuilder.DropColumn(
                name: "Tax",
                table: "StockFinancials");

            migrationBuilder.DropColumn(
                name: "TaxPercentage",
                table: "StockFinancials");
        }
    }
}
