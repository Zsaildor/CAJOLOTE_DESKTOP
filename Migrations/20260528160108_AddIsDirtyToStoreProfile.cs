using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cajolote.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDirtyToStoreProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDirty",
                table: "StoreProfiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDirty",
                table: "StoreProfiles");
        }
    }
}
