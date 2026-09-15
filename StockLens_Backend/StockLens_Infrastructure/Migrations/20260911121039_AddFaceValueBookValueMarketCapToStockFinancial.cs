using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockLens_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFaceValueBookValueMarketCapToStockFinancial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BookValue",
                table: "StockFinancials",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FaceValue",
                table: "StockFinancials",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MarketCap",
                table: "StockFinancials",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SectorPe",
                table: "StockFinancials",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalEquity",
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
                name: "BookValue",
                table: "StockFinancials");

            migrationBuilder.DropColumn(
                name: "FaceValue",
                table: "StockFinancials");

            migrationBuilder.DropColumn(
                name: "MarketCap",
                table: "StockFinancials");

            migrationBuilder.DropColumn(
                name: "SectorPe",
                table: "StockFinancials");

            migrationBuilder.DropColumn(
                name: "TotalEquity",
                table: "StockFinancials");
        }
    }
}
