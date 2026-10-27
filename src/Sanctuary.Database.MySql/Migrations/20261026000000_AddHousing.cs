using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.MySql.Migrations
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
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Definition = table.Column<int>(type: "int", nullable: false),
                    NameId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsLocked = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsFloraAllowed = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    PetAutospawn = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, defaultValueSql: "NOW()"),
                    LastVisited = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    CharacterId = table.Column<ulong>(type: "bigint unsigned", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "HouseFixtures",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Tint = table.Column<int>(type: "int", nullable: false),
                    Definition = table.Column<int>(type: "int", nullable: false),
                    PositionX = table.Column<float>(type: "float", nullable: false),
                    PositionY = table.Column<float>(type: "float", nullable: false),
                    PositionZ = table.Column<float>(type: "float", nullable: false),
                    RotationX = table.Column<float>(type: "float", nullable: false),
                    RotationY = table.Column<float>(type: "float", nullable: false),
                    RotationZ = table.Column<float>(type: "float", nullable: false),
                    RotationW = table.Column<float>(type: "float", nullable: false),
                    Scale = table.Column<float>(type: "float", nullable: false, defaultValue: 1f),
                    Created = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, defaultValueSql: "NOW()"),
                    HouseId = table.Column<ulong>(type: "bigint unsigned", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
