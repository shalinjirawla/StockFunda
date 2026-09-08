using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace StockLens_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockNews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Stocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Exchange = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Industry = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockNews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StockId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExternalNewsId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockNews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockNews_Stocks_StockId",
                        column: x => x.StockId,
                        principalTable: "Stocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Stocks",
                columns: new[] { "Id", "CompanyName", "CreatedAt", "Exchange", "Industry", "Symbol", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, "Reliance Industries Limited", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "NSE", "Oil & Gas / Conglomerate", "RELIANCE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, "Tata Consultancy Services Limited", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "NSE", "Information Technology", "TCS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, "Infosys Limited", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "NSE", "Information Technology", "INFY", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, "Tata Motors Limited", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "NSE", "Automobile", "TATAMOTORS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5, "HDFC Bank Limited", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "NSE", "Banking / Financial Services", "HDFCBANK", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 6, "ICICI Bank Limited", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "NSE", "Banking / Financial Services", "ICICIBANK", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 7, "Reliance Industries Limited", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "BSE", "Oil & Gas / Conglomerate", "RELIANCE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 8, "Tata Consultancy Services Limited", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "BSE", "Information Technology", "TCS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockNews_ExternalNewsId",
                table: "StockNews",
                column: "ExternalNewsId",
                unique: true,
                filter: "[ExternalNewsId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StockNews_PublishedAt",
                table: "StockNews",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_StockNews_StockId_PublishedAt",
                table: "StockNews",
                columns: new[] { "StockId", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StockNews_StockId_SourceUrl",
                table: "StockNews",
                columns: new[] { "StockId", "SourceUrl" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_Symbol_Exchange",
                table: "Stocks",
                columns: new[] { "Symbol", "Exchange" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockNews");

            migrationBuilder.DropTable(
                name: "Stocks");
        }
    }
}
