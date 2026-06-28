using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HamStats.Data.Migrations
{
    /// <inheritdoc />
    public partial class N1MMContactGridsquare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Gridsquare",
                table: "N1MMContacts",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Gridsquare",
                table: "N1MMContacts");
        }
    }
}
