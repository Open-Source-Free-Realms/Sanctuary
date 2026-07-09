using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.Sqlite.Migrations;

/// <inheritdoc />
public partial class AddModerationFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsMod",
            table: "Users",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<System.DateTimeOffset>(
            name: "LockedUntil",
            table: "Users",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<System.DateTimeOffset>(
            name: "MutedUntil",
            table: "Users",
            type: "TEXT",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsMod",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "LockedUntil",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "MutedUntil",
            table: "Users");
    }
}