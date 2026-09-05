using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gateway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OrderEventDetailAndObservabilityIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Detail",
                table: "OrderEvents",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_StoreId_PlacedAtUtc",
                table: "Orders",
                columns: new[] { "StoreId", "PlacedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderEvents_EventTimeUtc",
                table: "OrderEvents",
                column: "EventTimeUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OrderEvents_StoreId_EventTimeUtc",
                table: "OrderEvents",
                columns: new[] { "StoreId", "EventTimeUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_StoreId_PlacedAtUtc",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_OrderEvents_EventTimeUtc",
                table: "OrderEvents");

            migrationBuilder.DropIndex(
                name: "IX_OrderEvents_StoreId_EventTimeUtc",
                table: "OrderEvents");

            migrationBuilder.DropColumn(
                name: "Detail",
                table: "OrderEvents");
        }
    }
}
