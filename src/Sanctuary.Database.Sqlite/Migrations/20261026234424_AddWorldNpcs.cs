using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddWorldNpcs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorldEvents",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldEvents", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "WorldNpcs",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Zone = table.Column<int>(type: "INTEGER", nullable: false),
                    Definition = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    NameId = table.Column<int>(type: "INTEGER", nullable: false),
                    ModelId = table.Column<int>(type: "INTEGER", nullable: false),
                    Scale = table.Column<float>(type: "REAL", nullable: false, defaultValue: 1f),
                    Animation = table.Column<int>(type: "INTEGER", nullable: false),
                    Disposition = table.Column<int>(type: "INTEGER", nullable: false),
                    HideNamePlate = table.Column<bool>(type: "INTEGER", nullable: false),
                    Behavior = table.Column<int>(type: "INTEGER", nullable: false),
                    WanderRadius = table.Column<float>(type: "REAL", nullable: false),
                    Speed = table.Column<float>(type: "REAL", nullable: false),
                    PositionX = table.Column<float>(type: "REAL", nullable: false),
                    PositionY = table.Column<float>(type: "REAL", nullable: false),
                    PositionZ = table.Column<float>(type: "REAL", nullable: false),
                    RotationX = table.Column<float>(type: "REAL", nullable: false),
                    RotationY = table.Column<float>(type: "REAL", nullable: false),
                    RotationZ = table.Column<float>(type: "REAL", nullable: false),
                    RotationW = table.Column<float>(type: "REAL", nullable: false),
                    EventKey = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    CreatedBy = table.Column<ulong>(type: "INTEGER", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false, defaultValueSql: "DATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldNpcs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorldNpcs_Zone",
                table: "WorldNpcs",
                column: "Zone");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorldEvents");

            migrationBuilder.DropTable(
                name: "WorldNpcs");
        }
    }
}
