using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineShopUniPi.Migrations
{
    /// <inheritdoc />
    public partial class AddQuantityAndSizeToProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "quantity",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "size",
                table: "Products",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "quantity",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "size",
                table: "Products");
        }
    }
}
