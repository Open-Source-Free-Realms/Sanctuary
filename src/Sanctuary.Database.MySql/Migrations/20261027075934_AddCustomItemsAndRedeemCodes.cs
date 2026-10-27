using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomItemsAndRedeemCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BaseItemId = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Overrides = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedBy = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    Updated = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomItems", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RedeemCodes",
                columns: table => new
                {
                    Code = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Items = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Coins = table.Column<int>(type: "int", nullable: false),
                    MaxUses = table.Column<int>(type: "int", nullable: false),
                    Uses = table.Column<int>(type: "int", nullable: false),
                    PerCharacterLimit = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    MembersOnly = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    Note = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedBy = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RedeemCodes", x => x.Code);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RedeemCodeUses",
                columns: table => new
                {
                    Code = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CharacterId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    LastUsed = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RedeemCodeUses", x => new { x.Code, x.CharacterId });
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomItems");

            migrationBuilder.DropTable(
                name: "RedeemCodes");

            migrationBuilder.DropTable(
                name: "RedeemCodeUses");
        }
    }
}
