using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddHouses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Items_Tint_Definition_CharacterId",
                table: "Items");

            migrationBuilder.CreateTable(
                name: "Houses",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Key = table.Column<int>(type: "INTEGER", nullable: false),
                    ZoneId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsFloraAllowed = table.Column<bool>(type: "INTEGER", nullable: false),
                    PetAutospawn = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPublished = table.Column<bool>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Keywords = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false, defaultValueSql: "DATE()"),
                    CharacterId = table.Column<ulong>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Houses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Houses_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HousingGuids",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false, defaultValueSql: "DATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HousingGuids", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HouseVotes",
                columns: table => new
                {
                    HouseId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    VoterId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Rating = table.Column<float>(type: "REAL", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false, defaultValueSql: "DATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseVotes", x => new { x.HouseId, x.VoterId });
                    table.ForeignKey(
                        name: "FK_HouseVotes_Houses_HouseId",
                        column: x => x.HouseId,
                        principalTable: "Houses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Items_Tint_Definition_CharacterId",
                table: "Items",
                columns: new[] { "Tint", "Definition", "CharacterId" });

            migrationBuilder.CreateIndex(
                name: "IX_Houses_CharacterId_Key",
                table: "Houses",
                columns: new[] { "CharacterId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HouseVotes");

            migrationBuilder.DropTable(
                name: "HousingGuids");

            migrationBuilder.DropTable(
                name: "Houses");

            migrationBuilder.DropIndex(
                name: "IX_Items_Tint_Definition_CharacterId",
                table: "Items");

            migrationBuilder.CreateIndex(
                name: "IX_Items_Tint_Definition_CharacterId",
                table: "Items",
                columns: new[] { "Tint", "Definition", "CharacterId" },
                unique: true);
        }
    }
}
