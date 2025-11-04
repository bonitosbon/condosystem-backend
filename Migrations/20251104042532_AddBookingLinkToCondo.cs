using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoSystem_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingLinkToCondo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BookingLink",
                table: "Condos",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BookingLink",
                table: "Condos");
        }
    }
}
