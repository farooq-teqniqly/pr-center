using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrCenter.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPollDiagnostics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PollRuns",
                columns: table => new
                {
                    Id = table
                        .Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PollId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Outcome = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ConfiguredOwners = table.Column<string>(type: "TEXT", nullable: true),
                    OwnerCount = table.Column<int>(type: "INTEGER", nullable: true),
                    PublishedCount = table.Column<int>(type: "INTEGER", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollRuns", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "PollOwnerDiagnostics",
                columns: table => new
                {
                    Id = table
                        .Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PollRunId = table.Column<long>(type: "INTEGER", nullable: false),
                    Owner = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ResolvedLogin = table.Column<string>(
                        type: "TEXT",
                        maxLength: 255,
                        nullable: true
                    ),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Detail = table.Column<string>(type: "TEXT", nullable: true),
                    RequestedCount = table.Column<int>(type: "INTEGER", nullable: true),
                    ReviewedCount = table.Column<int>(type: "INTEGER", nullable: true),
                    UnionCount = table.Column<int>(type: "INTEGER", nullable: true),
                    DerivedCount = table.Column<int>(type: "INTEGER", nullable: true),
                    CarriedOverCount = table.Column<int>(type: "INTEGER", nullable: true),
                    DraftExclusions = table.Column<int>(type: "INTEGER", nullable: true),
                    ClosedOrMergedExclusions = table.Column<int>(type: "INTEGER", nullable: true),
                    ApprovedExclusions = table.Column<int>(type: "INTEGER", nullable: true),
                    UntrackedExclusions = table.Column<int>(type: "INTEGER", nullable: true),
                    RateLimitRemaining = table.Column<int>(type: "INTEGER", nullable: true),
                    RateLimitResetAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RateLimitCost = table.Column<int>(type: "INTEGER", nullable: true),
                    PullRequestIds = table.Column<string>(type: "TEXT", nullable: false),
                    ForeignItemCount = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollOwnerDiagnostics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PollOwnerDiagnostics_PollRuns_PollRunId",
                        column: x => x.PollRunId,
                        principalTable: "PollRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_PollOwnerDiagnostics_PollRunId",
                table: "PollOwnerDiagnostics",
                column: "PollRunId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_PollRuns_PollId",
                table: "PollRuns",
                column: "PollId",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PollOwnerDiagnostics");

            migrationBuilder.DropTable(name: "PollRuns");
        }
    }
}
