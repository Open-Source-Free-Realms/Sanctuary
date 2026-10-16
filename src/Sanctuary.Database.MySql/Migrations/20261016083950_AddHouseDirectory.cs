using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Houses",
                type: "varchar(256)",
                maxLength: 256,
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
                name: "Keywords",
                table: "Houses",
                type: "varchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublishedAt",
                table: "Houses",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HouseVotes",
                columns: table => new
                {
                    HouseId = table.Column<int>(type: "int", nullable: false),
                    VoterId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    CharacterId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    Rating = table.Column<float>(type: "float", nullable: false),
                    Category = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Created = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseVotes", x => new { x.HouseId, x.CharacterId, x.VoterId });
                    table.ForeignKey(
                        name: "FK_HouseVotes_Houses_HouseId_CharacterId",
                        columns: x => new { x.HouseId, x.CharacterId },
                        principalTable: "Houses",
                        principalColumns: new[] { "Id", "CharacterId" },
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
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
                name: "Keywords",
                table: "Houses");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "Houses");
        }
    }
}
