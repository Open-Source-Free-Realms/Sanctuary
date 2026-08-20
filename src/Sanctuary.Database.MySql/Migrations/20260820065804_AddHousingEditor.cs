using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.MySql.Migrations
{
    public partial class AddHousingEditor : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomizationData",
                table: "Houses",
                type: "TEXT",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "FurnitureScore",
                table: "Houses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsFloraAllowed",
                table: "Houses",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "Houses",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsMembersOnly",
                table: "Houses",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxFixtureCount",
                table: "Houses",
                type: "int",
                nullable: false,
                defaultValue: 2000);

            migrationBuilder.AddColumn<int>(
                name: "MaxLandmarkCount",
                table: "Houses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Houses",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "PetAutospawn",
                table: "Houses",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "HouseFixtures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    HouseId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    PlacementToken = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ItemDefinitionId = table.Column<int>(type: "int", nullable: false),
                    TintId = table.Column<int>(type: "int", nullable: false),
                    PositionX = table.Column<float>(type: "float", nullable: false),
                    PositionY = table.Column<float>(type: "float", nullable: false),
                    PositionZ = table.Column<float>(type: "float", nullable: false),
                    PositionW = table.Column<float>(type: "float", nullable: false),
                    RotationX = table.Column<float>(type: "float", nullable: false),
                    RotationY = table.Column<float>(type: "float", nullable: false),
                    RotationZ = table.Column<float>(type: "float", nullable: false),
                    RotationW = table.Column<float>(type: "float", nullable: false),
                    Scale = table.Column<float>(type: "float", nullable: false, defaultValue: 1f),
                    CustomizationData = table.Column<string>(type: "TEXT", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Created = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, defaultValueSql: "NOW()")
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
                name: "IX_HouseFixtures_HouseId_PlacementToken",
                table: "HouseFixtures",
                columns: new[] { "HouseId", "PlacementToken" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HouseFixtures");

            migrationBuilder.DropColumn(
                name: "CustomizationData",
                table: "Houses");

            migrationBuilder.DropColumn(
                name: "FurnitureScore",
                table: "Houses");

            migrationBuilder.DropColumn(
                name: "IsFloraAllowed",
                table: "Houses");

            migrationBuilder.DropColumn(
                name: "IsLocked",
                table: "Houses");

            migrationBuilder.DropColumn(
                name: "IsMembersOnly",
                table: "Houses");

            migrationBuilder.DropColumn(
                name: "MaxFixtureCount",
                table: "Houses");

            migrationBuilder.DropColumn(
                name: "MaxLandmarkCount",
                table: "Houses");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Houses");

            migrationBuilder.DropColumn(
                name: "PetAutospawn",
                table: "Houses");
        }
    }
}
