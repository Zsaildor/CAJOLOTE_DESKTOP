using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cajolote.Migrations
{
    /// <inheritdoc />
    public partial class AddPaidAmountToNote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "Notes",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "Notes");
        }
    }
}
