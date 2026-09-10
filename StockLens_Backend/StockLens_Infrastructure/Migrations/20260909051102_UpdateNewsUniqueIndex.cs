using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockLens_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateNewsUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockNews_ExternalNewsId",
                table: "StockNews");

            migrationBuilder.CreateIndex(
                name: "IX_StockNews_StockId_ExternalNewsId",
                table: "StockNews",
                columns: new[] { "StockId", "ExternalNewsId" },
                unique: true,
                filter: "[ExternalNewsId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockNews_StockId_ExternalNewsId",
                table: "StockNews");

            migrationBuilder.CreateIndex(
                name: "IX_StockNews_ExternalNewsId",
                table: "StockNews",
                column: "ExternalNewsId",
                unique: true,
                filter: "[ExternalNewsId] IS NOT NULL");
        }
    }
}
