using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MetaMeetDemo.Migrations
{
    public partial class AddAttendeesJson : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttendeesJson",
                table: "Meetings",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttendeesJson",
                table: "Meetings");
        }
    }
}
