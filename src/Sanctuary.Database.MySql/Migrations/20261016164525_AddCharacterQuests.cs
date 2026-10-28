using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterQuests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActiveQuestId",
                table: "Characters",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CharacterQuests",
                columns: table => new
                {
                    QuestId = table.Column<int>(type: "int", nullable: false),
                    CharacterId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    Completed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CurrentGoalId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterQuests", x => new { x.QuestId, x.CharacterId });
                    table.ForeignKey(
                        name: "FK_CharacterQuests_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CharacterQuestProgress",
                columns: table => new
                {
                    QuestId = table.Column<int>(type: "int", nullable: false),
                    CharacterId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    GoalId = table.Column<int>(type: "int", nullable: false),
                    TargetId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterQuestProgress", x => new { x.CharacterId, x.QuestId, x.GoalId, x.TargetId });
                    table.ForeignKey(
                        name: "FK_CharacterQuestProgress_CharacterQuests_QuestId_CharacterId",
                        columns: x => new { x.QuestId, x.CharacterId },
                        principalTable: "CharacterQuests",
                        principalColumns: new[] { "QuestId", "CharacterId" },
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterQuestProgress_QuestId_CharacterId",
                table: "CharacterQuestProgress",
                columns: new[] { "QuestId", "CharacterId" });

            migrationBuilder.CreateIndex(
                name: "IX_CharacterQuests_CharacterId",
                table: "CharacterQuests",
                column: "CharacterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CharacterQuestProgress");

            migrationBuilder.DropTable(
                name: "CharacterQuests");

            migrationBuilder.DropColumn(
                name: "ActiveQuestId",
                table: "Characters");
        }
    }
}
