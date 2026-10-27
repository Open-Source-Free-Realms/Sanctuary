using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddHousing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Houses",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Definition = table.Column<int>(type: "INTEGER", nullable: false),
                    NameId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsFloraAllowed = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    PetAutospawn = table.Column<bool>(type: "INTEGER", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false, defaultValueSql: "DATE()"),
                    LastVisited = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
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
                name: "HouseFixtures",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tint = table.Column<int>(type: "INTEGER", nullable: false),
                    Definition = table.Column<int>(type: "INTEGER", nullable: false),
                    PositionX = table.Column<float>(type: "REAL", nullable: false),
                    PositionY = table.Column<float>(type: "REAL", nullable: false),
                    PositionZ = table.Column<float>(type: "REAL", nullable: false),
                    RotationX = table.Column<float>(type: "REAL", nullable: false),
                    RotationY = table.Column<float>(type: "REAL", nullable: false),
                    RotationZ = table.Column<float>(type: "REAL", nullable: false),
                    RotationW = table.Column<float>(type: "REAL", nullable: false),
                    Scale = table.Column<float>(type: "REAL", nullable: false, defaultValue: 1f),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false, defaultValueSql: "DATE()"),
                    HouseId = table.Column<ulong>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseFixtures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseFixtures_Houses_HouseId",
                        column: x => x.HouseId,
                        principalTable: "Houses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HouseFixtures_HouseId",
                table: "HouseFixtures",
                column: "HouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Houses_CharacterId",
                table: "Houses",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_Houses_Definition_CharacterId",
                table: "Houses",
                columns: new[] { "Definition", "CharacterId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HouseFixtures");

            migrationBuilder.DropTable(
                name: "Houses");
        }
    }
}
