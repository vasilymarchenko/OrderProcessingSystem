using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace InventoryService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AvailableQuantity = table.Column<int>(type: "integer", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "InventoryItems",
                columns: new[] { "Id", "AvailableQuantity", "LastUpdated", "ProductCode" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), 100, new DateTime(2025, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc), "PROD-001" },
                    { new Guid("22222222-2222-2222-2222-222222222222"), 50, new DateTime(2025, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc), "PROD-002" },
                    { new Guid("33333333-3333-3333-3333-333333333333"), 0, new DateTime(2025, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc), "PROD-003" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_ProductCode",
                table: "InventoryItems",
                column: "ProductCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryItems");
        }
    }
}
