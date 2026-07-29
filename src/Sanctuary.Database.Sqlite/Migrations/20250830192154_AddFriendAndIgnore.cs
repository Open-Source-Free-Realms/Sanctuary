using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddFriendAndIgnore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "Characters",
                type: "TEXT",
                maxLength: 32,
                nullable: true,
                computedColumnSql: "CONCAT_WS(' ', FirstName, NULLIF(LastName, ''))",
                stored: true);

            migrationBuilder.CreateTable(
                name: "Friends",
                columns: table => new
                {
                    FriendCharacterGuid = table.Column<ulong>(type: "INTEGER", nullable: false),
                    CharacterGuid = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false, defaultValueSql: "DATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Friends", x => new { x.FriendCharacterGuid, x.CharacterGuid });
                    table.ForeignKey(
                        name: "FK_Friends_Characters_CharacterGuid",
                        column: x => x.CharacterGuid,
                        principalTable: "Characters",
                        principalColumn: "Guid",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Friends_Characters_FriendCharacterGuid",
                        column: x => x.FriendCharacterGuid,
                        principalTable: "Characters",
                        principalColumn: "Guid");
                });

            migrationBuilder.CreateTable(
                name: "Ignores",
                columns: table => new
                {
                    IgnoreCharacterGuid = table.Column<ulong>(type: "INTEGER", nullable: false),
                    CharacterGuid = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false, defaultValueSql: "DATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ignores", x => new { x.IgnoreCharacterGuid, x.CharacterGuid });
                    table.ForeignKey(
                        name: "FK_Ignores_Characters_CharacterGuid",
                        column: x => x.CharacterGuid,
                        principalTable: "Characters",
                        principalColumn: "Guid",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Ignores_Characters_IgnoreCharacterGuid",
                        column: x => x.IgnoreCharacterGuid,
                        principalTable: "Characters",
                        principalColumn: "Guid");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Friends_CharacterGuid",
                table: "Friends",
                column: "CharacterGuid");

            migrationBuilder.CreateIndex(
                name: "IX_Ignores_CharacterGuid",
                table: "Ignores",
                column: "CharacterGuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Friends");

            migrationBuilder.DropTable(
                name: "Ignores");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "Characters");
        }
    }
}
