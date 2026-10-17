using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kanimambo.Ordering.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderPaymentAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PaymentAttempts",
                schema: "ordering",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentAttempts",
                schema: "ordering",
                table: "Orders");
        }
    }
}
