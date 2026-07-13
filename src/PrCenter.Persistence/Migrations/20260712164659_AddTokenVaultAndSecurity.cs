using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrCenter.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTokenVaultAndSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSecurity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Salt = table.Column<byte[]>(type: "BLOB", nullable: false),
                    MemoryKib = table.Column<int>(type: "INTEGER", nullable: false),
                    Iterations = table.Column<int>(type: "INTEGER", nullable: false),
                    Parallelism = table.Column<int>(type: "INTEGER", nullable: false),
                    KdfVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    SentinelNonce = table.Column<byte[]>(type: "BLOB", nullable: false),
                    SentinelCiphertext = table.Column<byte[]>(type: "BLOB", nullable: false),
                    SentinelTag = table.Column<byte[]>(type: "BLOB", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSecurity", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "OwnerTokens",
                columns: table => new
                {
                    Owner = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Nonce = table.Column<byte[]>(type: "BLOB", nullable: false),
                    Ciphertext = table.Column<byte[]>(type: "BLOB", nullable: false),
                    Tag = table.Column<byte[]>(type: "BLOB", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OwnerTokens", x => x.Owner);
                }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AppSecurity");

            migrationBuilder.DropTable(name: "OwnerTokens");
        }
    }
}
