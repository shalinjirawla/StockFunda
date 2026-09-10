using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockLens_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockShareholding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockShareholdings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StockId = table.Column<int>(type: "int", nullable: false),
                    PeriodKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Period = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PeriodDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PeriodType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PromoterHolding = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    FiiHolding = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    DiiHolding = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    PublicHolding = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockShareholdings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockShareholdings_Stocks_StockId",
                        column: x => x.StockId,
                        principalTable: "Stocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockShareholdings_StockId_LastSyncedAt",
                table: "StockShareholdings",
                columns: new[] { "StockId", "LastSyncedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StockShareholdings_StockId_PeriodDate",
                table: "StockShareholdings",
                columns: new[] { "StockId", "PeriodDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StockShareholdings_StockId_PeriodKey",
                table: "StockShareholdings",
                columns: new[] { "StockId", "PeriodKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockShareholdings");
        }
    }
}
