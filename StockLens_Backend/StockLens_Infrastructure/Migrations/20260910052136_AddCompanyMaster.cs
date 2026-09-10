using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace StockLens_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Stocks",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Stocks",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Stocks",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Stocks",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Stocks",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Stocks",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Stocks",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Stocks",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "Stocks");

            migrationBuilder.DropColumn(
                name: "Industry",
                table: "Stocks");

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "Stocks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CompanyMaster",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Industry = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LogoUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyMaster", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_CompanyId",
                table: "Stocks",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Company_Symbol",
                table: "CompanyMaster",
                column: "Symbol",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Stocks_CompanyMaster_CompanyId",
                table: "Stocks",
                column: "CompanyId",
                principalTable: "CompanyMaster",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stocks_CompanyMaster_CompanyId",
                table: "Stocks");

            migrationBuilder.DropTable(
                name: "CompanyMaster");

            migrationBuilder.DropIndex(
                name: "IX_Stocks_CompanyId",
                table: "Stocks");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Stocks");

            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "Stocks",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Industry",
                table: "Stocks",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

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
        }
    }
}
