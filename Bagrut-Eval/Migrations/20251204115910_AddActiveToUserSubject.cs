using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bagrut_Eval.Migrations
{
    /// <inheritdoc />
    public partial class AddActiveToUserSubject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Active",
                table: "UserSubjects",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Active",
                table: "UserSubjects");
        }
    }
}
