using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.MySql.Migrations
{
    public partial class AddHousingDirectory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Houses",
                type: "longtext",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "Houses",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "KeywordList",
                table: "Houses",
                type: "longtext",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastVisited",
                table: "Houses",
                type: "datetime(6)",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<float>(
                name: "Rating",
                table: "Houses",
                type: "float",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<int>(
                name: "Votes",
                table: "Houses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "HouseVotes",
                columns: table => new
                {
                    HouseId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    CharacterId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    Value = table.Column<int>(type: "int", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseVotes", x => new { x.HouseId, x.CharacterId });
                    table.ForeignKey(
                        name: "FK_HouseVotes_Houses_HouseId",
                        column: x => x.HouseId,
                        principalTable: "Houses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HouseVotes");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Houses");

            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "Houses");

            migrationBuilder.DropColumn(
                name: "KeywordList",
                table: "Houses");

            migrationBuilder.DropColumn(
                name: "LastVisited",
                table: "Houses");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "Houses");

            migrationBuilder.DropColumn(
                name: "Votes",
                table: "Houses");
        }
    }
}
