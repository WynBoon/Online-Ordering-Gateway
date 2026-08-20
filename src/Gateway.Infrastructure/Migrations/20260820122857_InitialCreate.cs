using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gateway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BillingRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanType = table.Column<int>(type: "int", nullable: false),
                    RateCents = table.Column<long>(type: "bigint", nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChannelConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelType = table.Column<int>(type: "int", nullable: false),
                    LocationKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PreviousLocationKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LocationKeyRotatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    WebhookUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SigningSecretRef = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                columns: table => new
                {
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "int", nullable: false),
                    ResponseBodyJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.IdempotencyKey);
                });

            migrationBuilder.CreateTable(
                name: "OrderEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderRef = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EventId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    EventTimeUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    InternalId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderRef = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DisplayId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceChannel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BrandName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FulfillmentType = table.Column<int>(type: "int", nullable: false),
                    PlacedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ScheduledForUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SubtotalCents = table.Column<long>(type: "bigint", nullable: false),
                    TaxCents = table.Column<long>(type: "bigint", nullable: false),
                    DeliveryFeeCents = table.Column<long>(type: "bigint", nullable: false),
                    TipCents = table.Column<long>(type: "bigint", nullable: false),
                    TotalCents = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Prepaid = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CancelReason = table.Column<int>(type: "int", nullable: true),
                    PosOrderId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Customer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveryAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Items = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.InternalId);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MessageType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DispatchedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PosConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PosType = table.Column<int>(type: "int", nullable: false),
                    ExternalNodeId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExternalLocationId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecretRef = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExtraConfig = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Timezone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StateChangedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StateChangeReason = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stores", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillingRates_StoreId_EffectiveFrom",
                table: "BillingRates",
                columns: new[] { "StoreId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelConnections_LocationKey",
                table: "ChannelConnections",
                column: "LocationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChannelConnections_StoreId",
                table: "ChannelConnections",
                column: "StoreId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderEvents_EventId",
                table: "OrderEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderEvents_OrderRef",
                table: "OrderEvents",
                column: "OrderRef");

            migrationBuilder.CreateIndex(
                name: "IX_OrderEvents_StoreId",
                table: "OrderEvents",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderRef",
                table: "Orders",
                column: "OrderRef",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_DispatchedAtUtc",
                table: "OutboxMessages",
                column: "DispatchedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PosConnections_StoreId",
                table: "PosConnections",
                column: "StoreId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stores_GroupId",
                table: "Stores",
                column: "GroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillingRates");

            migrationBuilder.DropTable(
                name: "ChannelConnections");

            migrationBuilder.DropTable(
                name: "Groups");

            migrationBuilder.DropTable(
                name: "IdempotencyRecords");

            migrationBuilder.DropTable(
                name: "OrderEvents");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "PosConnections");

            migrationBuilder.DropTable(
                name: "Stores");
        }
    }
}
