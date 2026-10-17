using System;
using System.Collections.Generic;
using Kanimambo.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kanimambo.Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "BackofficeOperations",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    PerformedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PerformedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    UndoneBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UndoneAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackofficeOperations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogItems",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Stock = table.Column<int>(type: "integer", nullable: true),
                    UnitWeightKg = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    TaxCategory = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Variant = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PriceAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PriceCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboxRecords",
                schema: "catalog",
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
                name: "Sellers",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Rating = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SuspensionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DeliveryCities = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sellers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BundleComponents",
                schema: "catalog",
                columns: table => new
                {
                    ComponentId = table.Column<Guid>(type: "uuid", nullable: false),
                    BundleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BundleComponents", x => new { x.BundleId, x.ComponentId });
                    table.ForeignKey(
                        name: "FK_BundleComponents_CatalogItems_BundleId",
                        column: x => x.BundleId,
                        principalSchema: "catalog",
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BundleComponents_CatalogItems_ComponentId",
                        column: x => x.ComponentId,
                        principalSchema: "catalog",
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackofficeOperations_PerformedAt",
                schema: "catalog",
                table: "BackofficeOperations",
                column: "PerformedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BundleComponents_ComponentId",
                schema: "catalog",
                table: "BundleComponents",
                column: "ComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_Category",
                schema: "catalog",
                table: "CatalogItems",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_SellerId_Sku",
                schema: "catalog",
                table: "CatalogItems",
                columns: new[] { "SellerId", "Sku" },
                unique: true);

            // Search read model (recursive CTE over the bundle tree), mapped by CatalogDbContext.CatalogEntries.
            migrationBuilder.Sql(CatalogEntriesViewSql.Create);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CatalogEntriesViewSql.Drop);

            migrationBuilder.DropTable(
                name: "BackofficeOperations",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "BundleComponents",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "InboxRecords",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Sellers",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "CatalogItems",
                schema: "catalog");
        }
    }
}
