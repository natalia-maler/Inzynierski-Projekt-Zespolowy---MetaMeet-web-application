using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MetaMeetDemo.Migrations
{
    public partial class RemoveSelectedCalendarId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SelectedCalendarId",
                table: "AppUsers");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SelectedCalendarId",
                table: "AppUsers",
                type: "TEXT",
                maxLength: 255,
                nullable: true);
        }
    }
}
