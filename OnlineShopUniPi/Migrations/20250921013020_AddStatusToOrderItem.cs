using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineShopUniPi.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusToOrderItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "OrderItems",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "status",
                table: "OrderItems");
        }
    }
}
