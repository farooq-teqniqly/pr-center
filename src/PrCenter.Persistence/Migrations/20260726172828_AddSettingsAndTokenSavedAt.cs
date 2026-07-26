using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrCenter.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSettingsAndTokenSavedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SavedAt",
                table: "OwnerTokens",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    PollIntervalSeconds = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AppSettings");

            migrationBuilder.DropColumn(name: "SavedAt", table: "OwnerTokens");
        }
    }
}
