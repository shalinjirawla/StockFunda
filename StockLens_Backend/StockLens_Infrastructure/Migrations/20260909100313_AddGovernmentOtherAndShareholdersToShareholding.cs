using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockLens_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGovernmentOtherAndShareholdersToShareholding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GovernmentHolding",
                table: "StockShareholdings",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OtherHolding",
                table: "StockShareholdings",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ShareholdersCount",
                table: "StockShareholdings",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GovernmentHolding",
                table: "StockShareholdings");

            migrationBuilder.DropColumn(
                name: "OtherHolding",
                table: "StockShareholdings");

            migrationBuilder.DropColumn(
                name: "ShareholdersCount",
                table: "StockShareholdings");
        }
    }
}
