using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.Sqlite.Migrations
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
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "Houses",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Keywords",
                table: "Houses",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublishedAt",
                table: "Houses",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HouseVotes",
                columns: table => new
                {
                    HouseId = table.Column<int>(type: "INTEGER", nullable: false),
                    VoterId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    CharacterId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Rating = table.Column<float>(type: "REAL", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false, defaultValueSql: "DATE()")
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
                });
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
