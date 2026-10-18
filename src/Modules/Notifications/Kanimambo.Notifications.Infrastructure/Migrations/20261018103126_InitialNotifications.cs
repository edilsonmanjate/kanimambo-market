using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kanimambo.Notifications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "notifications");

            migrationBuilder.CreateTable(
                name: "ContactPreferences",
                schema: "notifications",
                columns: table => new
                {
                    Phone = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    Channels = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactPreferences", x => x.Phone);
                });

            migrationBuilder.CreateTable(
                name: "InboxRecords",
                schema: "notifications",
                columns: table => new
                {
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    CommandName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxRecords", x => x.IdempotencyKey);
                });

            migrationBuilder.CreateTable(
                name: "SentNotifications",
                schema: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Recipient = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    OrderNumber = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Text = table.Column<string>(type: "text", nullable: true),
                    ProviderMessageId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SentNotifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SentNotifications_EventId",
                schema: "notifications",
                table: "SentNotifications",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_SentNotifications_OrderNumber",
                schema: "notifications",
                table: "SentNotifications",
                column: "OrderNumber");

            migrationBuilder.CreateIndex(
                name: "IX_SentNotifications_Recipient",
                schema: "notifications",
                table: "SentNotifications",
                column: "Recipient");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContactPreferences",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "InboxRecords",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "SentNotifications",
                schema: "notifications");
        }
    }
}
