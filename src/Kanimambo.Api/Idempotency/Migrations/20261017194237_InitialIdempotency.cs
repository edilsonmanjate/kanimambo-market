using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kanimambo.Api.Idempotency.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "platform");

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                schema: "platform",
                columns: table => new
                {
                    Scope = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "integer", nullable: true),
                    ResponseContentType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ResponseLocation = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ResponseBody = table.Column<byte[]>(type: "bytea", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => new { x.Scope, x.Key });
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_ExpiresAt",
                schema: "platform",
                table: "IdempotencyRecords",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdempotencyRecords",
                schema: "platform");
        }
    }
}
