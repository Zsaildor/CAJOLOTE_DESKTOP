using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cajolote.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditFieldsToHistoricalSale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EditedAt",
                table: "HistoricalSales",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEdited",
                table: "HistoricalSales",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EditedAt",
                table: "HistoricalSales");

            migrationBuilder.DropColumn(
                name: "IsEdited",
                table: "HistoricalSales");
        }
    }
}
