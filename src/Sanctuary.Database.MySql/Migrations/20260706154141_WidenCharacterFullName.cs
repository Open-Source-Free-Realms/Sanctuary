using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.MySql.Migrations
{
    /// <inheritdoc />
    public partial class WidenCharacterFullName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "Characters",
                type: "varchar(33)",
                maxLength: 33,
                nullable: true,
                computedColumnSql: "CONCAT_WS(' ', `FirstName`, NULLIF(`LastName`, ''))",
                stored: true,
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldNullable: true,
                oldComputedColumnSql: "CONCAT_WS(' ', `FirstName`, NULLIF(`LastName`, ''))",
                oldStored: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "Characters",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true,
                computedColumnSql: "CONCAT_WS(' ', `FirstName`, NULLIF(`LastName`, ''))",
                stored: true,
                oldClrType: typeof(string),
                oldType: "varchar(33)",
                oldMaxLength: 33,
                oldNullable: true,
                oldComputedColumnSql: "CONCAT_WS(' ', `FirstName`, NULLIF(`LastName`, ''))",
                oldStored: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
