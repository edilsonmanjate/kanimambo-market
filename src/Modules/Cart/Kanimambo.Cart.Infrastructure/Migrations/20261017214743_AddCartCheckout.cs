using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kanimambo.Cart.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCartCheckout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CheckedOutOrderId",
                schema: "cart",
                table: "Carts",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheckedOutOrderId",
                schema: "cart",
                table: "Carts");
        }
    }
}
