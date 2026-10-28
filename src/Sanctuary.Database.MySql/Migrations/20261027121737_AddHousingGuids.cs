using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddHousingGuids : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HouseVotes_Houses_HouseId_CharacterId",
                table: "HouseVotes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_HouseVotes",
                table: "HouseVotes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Houses",
                table: "Houses");

            migrationBuilder.AlterColumn<ulong>(
                name: "HouseId",
                table: "HouseVotes",
                type: "bigint unsigned",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<ulong>(
                name: "Id",
                table: "Houses",
                type: "bigint unsigned",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "Key",
                table: "Houses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE Houses SET `Key` = Id;");
            migrationBuilder.Sql("UPDATE Houses JOIN (SELECT CharacterId, `Key`, ROW_NUMBER() OVER (ORDER BY CharacterId, `Key`) AS NewId FROM Houses) AS HouseIds ON HouseIds.CharacterId = Houses.CharacterId AND HouseIds.`Key` = Houses.`Key` SET Houses.Id = HouseIds.NewId;");
            migrationBuilder.Sql("UPDATE HouseVotes JOIN Houses ON Houses.CharacterId = HouseVotes.CharacterId AND Houses.`Key` = HouseVotes.HouseId SET HouseVotes.HouseId = Houses.Id;");

            migrationBuilder.DropColumn(
                name: "CharacterId",
                table: "HouseVotes");

            migrationBuilder.AddPrimaryKey(
                name: "PK_HouseVotes",
                table: "HouseVotes",
                columns: new[] { "HouseId", "VoterId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Houses",
                table: "Houses",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "HousingGuids",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Created = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HousingGuids", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql("INSERT INTO HousingGuids (Id, Created) SELECT Id, Created FROM Houses;");

            migrationBuilder.CreateIndex(
                name: "IX_Houses_CharacterId_Key",
                table: "Houses",
                columns: new[] { "CharacterId", "Key" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_Houses_CharacterId",
                table: "Houses");

            migrationBuilder.AddForeignKey(
                name: "FK_HouseVotes_Houses_HouseId",
                table: "HouseVotes",
                column: "HouseId",
                principalTable: "Houses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HouseVotes_Houses_HouseId",
                table: "HouseVotes");

            migrationBuilder.DropTable(
                name: "HousingGuids");

            migrationBuilder.DropPrimaryKey(
                name: "PK_HouseVotes",
                table: "HouseVotes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Houses",
                table: "Houses");

            migrationBuilder.AddColumn<ulong>(
                name: "CharacterId",
                table: "HouseVotes",
                type: "bigint unsigned",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.Sql("UPDATE HouseVotes JOIN Houses ON Houses.Id = HouseVotes.HouseId SET HouseVotes.CharacterId = Houses.CharacterId, HouseVotes.HouseId = Houses.`Key`;");
            migrationBuilder.Sql("UPDATE Houses SET Id = `Key`;");

            migrationBuilder.CreateIndex(
                name: "IX_Houses_CharacterId",
                table: "Houses",
                column: "CharacterId");

            migrationBuilder.DropIndex(
                name: "IX_Houses_CharacterId_Key",
                table: "Houses");

            migrationBuilder.DropColumn(
                name: "Key",
                table: "Houses");

            migrationBuilder.AlterColumn<int>(
                name: "HouseId",
                table: "HouseVotes",
                type: "int",
                nullable: false,
                oldClrType: typeof(ulong),
                oldType: "bigint unsigned");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Houses",
                type: "int",
                nullable: false,
                oldClrType: typeof(ulong),
                oldType: "bigint unsigned");

            migrationBuilder.AddPrimaryKey(
                name: "PK_HouseVotes",
                table: "HouseVotes",
                columns: new[] { "HouseId", "CharacterId", "VoterId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Houses",
                table: "Houses",
                columns: new[] { "Id", "CharacterId" });

            migrationBuilder.AddForeignKey(
                name: "FK_HouseVotes_Houses_HouseId_CharacterId",
                table: "HouseVotes",
                columns: new[] { "HouseId", "CharacterId" },
                principalTable: "Houses",
                principalColumns: new[] { "Id", "CharacterId" },
                onDelete: ReferentialAction.Cascade);
        }
    }
}
