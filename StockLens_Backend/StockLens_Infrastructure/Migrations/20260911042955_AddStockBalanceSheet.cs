using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockLens_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockBalanceSheet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockBalanceSheets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StockId = table.Column<int>(type: "int", nullable: false),
                    PeriodKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PeriodType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FiscalYear = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PeriodEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConsolidationType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EquityCapital = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Reserves = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Borrowings = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OtherLiabilities = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalLiabilities = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FixedAssets = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Cwip = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Investments = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OtherAssets = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalAssets = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockBalanceSheets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockBalanceSheets_Stocks_StockId",
                        column: x => x.StockId,
                        principalTable: "Stocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockBalanceSheets_StockId",
                table: "StockBalanceSheets",
                column: "StockId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockBalanceSheets");
        }
    }
}
