using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gateway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StoreDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefaultDeviceFunctions",
                table: "Stores",
                type: "int",
                nullable: false,
                defaultValue: 127);

            migrationBuilder.CreateTable(
                name: "StoreDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Functions = table.Column<int>(type: "int", nullable: false),
                    EnrollmentCodeHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    EnrollmentExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    HardwareFingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EnrolledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastHeartbeatAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreDevices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoreDevices_EnrollmentCodeHash",
                table: "StoreDevices",
                column: "EnrollmentCodeHash");

            migrationBuilder.CreateIndex(
                name: "IX_StoreDevices_StoreId",
                table: "StoreDevices",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreDevices_TokenHash",
                table: "StoreDevices",
                column: "TokenHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoreDevices");

            migrationBuilder.DropColumn(
                name: "DefaultDeviceFunctions",
                table: "Stores");
        }
    }
}
