using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kanimambo.Shipping.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialShipping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "shipping");

            migrationBuilder.CreateTable(
                name: "Consignments",
                schema: "shipping",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<long>(type: "bigint", nullable: false),
                    Method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DestinationCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    District = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PickupPointId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RecipientPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Consignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboxRecords",
                schema: "shipping",
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
                name: "OutboxMessages",
                schema: "shipping",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LockedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DeliveredTo = table.Column<List<string>>(type: "text[]", nullable: false),
                    DeadLettered = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Parcels",
                schema: "shipping",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WeightKg = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Courier = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Vehicle = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TrackingCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ConsignmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CashToCollectMzn = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcels_Consignments_ConsignmentId",
                        column: x => x.ConsignmentId,
                        principalSchema: "shipping",
                        principalTable: "Consignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParcelTracking",
                schema: "shipping",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ParcelId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParcelTracking", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParcelTracking_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalSchema: "shipping",
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Consignments_OrderId",
                schema: "shipping",
                table: "Consignments",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedAt_NextAttemptAt",
                schema: "shipping",
                table: "OutboxMessages",
                columns: new[] { "ProcessedAt", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ParcelTracking_ParcelId",
                schema: "shipping",
                table: "ParcelTracking",
                column: "ParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_ConsignmentId",
                schema: "shipping",
                table: "Parcels",
                column: "ConsignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_Reference",
                schema: "shipping",
                table: "Parcels",
                column: "Reference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboxRecords",
                schema: "shipping");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "shipping");

            migrationBuilder.DropTable(
                name: "ParcelTracking",
                schema: "shipping");

            migrationBuilder.DropTable(
                name: "Parcels",
                schema: "shipping");

            migrationBuilder.DropTable(
                name: "Consignments",
                schema: "shipping");
        }
    }
}
