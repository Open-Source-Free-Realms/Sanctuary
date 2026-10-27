using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddScriptLibraryAndCustomQuests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomQuests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    TitleId = table.Column<int>(type: "INTEGER", nullable: false),
                    DescriptionId = table.Column<int>(type: "INTEGER", nullable: false),
                    GiverNpcId = table.Column<int>(type: "INTEGER", nullable: false),
                    TurnInNpcId = table.Column<int>(type: "INTEGER", nullable: false),
                    RewardCoins = table.Column<int>(type: "INTEGER", nullable: false),
                    PrerequisiteQuestId = table.Column<int>(type: "INTEGER", nullable: true),
                    NextQuestId = table.Column<int>(type: "INTEGER", nullable: true),
                    Goals = table.Column<string>(type: "TEXT", nullable: false),
                    Rewards = table.Column<string>(type: "TEXT", nullable: false),
                    IsPublished = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedBy = table.Column<ulong>(type: "INTEGER", nullable: true),
                    Updated = table.Column<DateTimeOffset>(type: "TEXT", nullable: false, defaultValueSql: "DATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomQuests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScriptLibrary",
                columns: table => new
                {
                    Name = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    UpdatedBy = table.Column<ulong>(type: "INTEGER", nullable: true),
                    Updated = table.Column<DateTimeOffset>(type: "TEXT", nullable: false, defaultValueSql: "DATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScriptLibrary", x => x.Name);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomQuests");

            migrationBuilder.DropTable(
                name: "ScriptLibrary");
        }
    }
}
