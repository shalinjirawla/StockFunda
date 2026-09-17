using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockLens_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedBorrowingInBalanceSheet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LongTermBorrowings",
                table: "StockBalanceSheets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShortTermBorrowings",
                table: "StockBalanceSheets",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LongTermBorrowings",
                table: "StockBalanceSheets");

            migrationBuilder.DropColumn(
                name: "ShortTermBorrowings",
                table: "StockBalanceSheets");
        }
    }
}
