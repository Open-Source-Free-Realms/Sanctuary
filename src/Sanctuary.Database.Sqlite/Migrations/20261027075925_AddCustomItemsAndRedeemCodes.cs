using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.Sqlite.Migrations
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
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BaseItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Overrides = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedBy = table.Column<ulong>(type: "INTEGER", nullable: true),
                    Updated = table.Column<DateTimeOffset>(type: "TEXT", nullable: false, defaultValueSql: "DATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RedeemCodes",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Items = table.Column<string>(type: "TEXT", nullable: false),
                    Coins = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxUses = table.Column<int>(type: "INTEGER", nullable: false),
                    Uses = table.Column<int>(type: "INTEGER", nullable: false),
                    PerCharacterLimit = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    StartsAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    MembersOnly = table.Column<bool>(type: "INTEGER", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedBy = table.Column<ulong>(type: "INTEGER", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false, defaultValueSql: "DATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RedeemCodes", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "RedeemCodeUses",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CharacterId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Count = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUsed = table.Column<DateTimeOffset>(type: "TEXT", nullable: false, defaultValueSql: "DATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RedeemCodeUses", x => new { x.Code, x.CharacterId });
                });
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
