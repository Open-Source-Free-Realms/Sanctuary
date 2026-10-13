using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterCollectionEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CollectionEntries",
                columns: table => new
                {
                    CollectionId = table.Column<int>(type: "INTEGER", nullable: false),
                    EntryId = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacterId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Collected = table.Column<DateTimeOffset>(type: "TEXT", nullable: false, defaultValueSql: "DATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionEntries", x => new { x.CharacterId, x.CollectionId, x.EntryId });
                    table.ForeignKey(
                        name: "FK_CollectionEntries_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollectionEntries");
        }
    }
}
