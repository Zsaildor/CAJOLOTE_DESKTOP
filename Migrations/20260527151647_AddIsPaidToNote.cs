using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cajolote.Migrations
{
    /// <inheritdoc />
    public partial class AddIsPaidToNote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPaid",
                table: "Notes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPaid",
                table: "Notes");
        }
    }
}
