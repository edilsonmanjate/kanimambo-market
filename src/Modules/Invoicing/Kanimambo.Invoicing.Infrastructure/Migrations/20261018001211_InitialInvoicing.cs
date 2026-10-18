using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kanimambo.Invoicing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialInvoicing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "invoicing");

            migrationBuilder.CreateTable(
                name: "DocumentCounters",
                schema: "invoicing",
                columns: table => new
                {
                    IssuerNuit = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    Series = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    LastNumber = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentCounters", x => new { x.IssuerNuit, x.Series, x.Year });
                });

            migrationBuilder.CreateTable(
                name: "FiscalDocuments",
                schema: "invoicing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Regime = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Series = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    FullNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LegalNotice = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IssuerNuit = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    IssuerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IssuerAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    FiscalDocument_IssuerNuit = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    BuyerNuit = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    BuyerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BuyerAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<long>(type: "bigint", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Net = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Iva = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Gross = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreditedInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreditedInvoiceNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboxRecords",
                schema: "invoicing",
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
                name: "InvoiceRequests",
                schema: "invoicing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<long>(type: "bigint", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerNuit = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    BuyerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BuyerAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    BuyerNuitRequired = table.Column<bool>(type: "boolean", nullable: false),
                    CustomerPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Lines = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Problem = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "invoicing",
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
                name: "Taxpayers",
                schema: "invoicing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nuit = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    LegalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Regime = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Taxpayers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FiscalLines",
                schema: "invoicing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    TaxCategory = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    Discount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Net = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Iva = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Gross = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExemptionReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FiscalLines_FiscalDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "invoicing",
                        principalTable: "FiscalDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FiscalDocuments_CreditedInvoiceId",
                schema: "invoicing",
                table: "FiscalDocuments",
                column: "CreditedInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalDocuments_FiscalDocument_IssuerNuit_IssueDate",
                schema: "invoicing",
                table: "FiscalDocuments",
                columns: new[] { "FiscalDocument_IssuerNuit", "IssueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FiscalDocuments_FiscalDocument_IssuerNuit_Series_Year_Number",
                schema: "invoicing",
                table: "FiscalDocuments",
                columns: new[] { "FiscalDocument_IssuerNuit", "Series", "Year", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FiscalDocuments_OrderId",
                schema: "invoicing",
                table: "FiscalDocuments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalLines_DocumentId",
                schema: "invoicing",
                table: "FiscalLines",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRequests_OrderId_SellerId",
                schema: "invoicing",
                table: "InvoiceRequests",
                columns: new[] { "OrderId", "SellerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRequests_SellerId_Status",
                schema: "invoicing",
                table: "InvoiceRequests",
                columns: new[] { "SellerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedAt_NextAttemptAt",
                schema: "invoicing",
                table: "OutboxMessages",
                columns: new[] { "ProcessedAt", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Taxpayers_Nuit",
                schema: "invoicing",
                table: "Taxpayers",
                column: "Nuit",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentCounters",
                schema: "invoicing");

            migrationBuilder.DropTable(
                name: "FiscalLines",
                schema: "invoicing");

            migrationBuilder.DropTable(
                name: "InboxRecords",
                schema: "invoicing");

            migrationBuilder.DropTable(
                name: "InvoiceRequests",
                schema: "invoicing");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "invoicing");

            migrationBuilder.DropTable(
                name: "Taxpayers",
                schema: "invoicing");

            migrationBuilder.DropTable(
                name: "FiscalDocuments",
                schema: "invoicing");
        }
    }
}
