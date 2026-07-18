using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrCenter.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropLastSeenMarkers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "LastSeenMarkers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LastSeenMarkers",
                columns: table => new
                {
                    PullRequestId = table.Column<string>(
                        type: "TEXT",
                        maxLength: 255,
                        nullable: false
                    ),
                    SeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LastSeenMarkers", x => x.PullRequestId);
                }
            );
        }
    }
}
