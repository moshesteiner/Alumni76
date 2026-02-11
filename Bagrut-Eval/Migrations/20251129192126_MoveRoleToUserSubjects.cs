using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bagrut_Eval.Migrations
{
    /// <inheritdoc />
    public partial class MoveRoleToUserSubjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "UserSubjects",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(@$"
                UPDATE UserSubjects us
                INNER JOIN Users u ON us.UserId = u.Id
                SET us.Role = u.Role;
                ");

            migrationBuilder.DropColumn(
               name: "Role",
               table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Users",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(@$"
                UPDATE Users u
                INNER JOIN UserSubjects us ON us.UserId = u.Id
                SET u.Role = us.Role;
                ");
            migrationBuilder.DropColumn(
               name: "Role",
               table: "UserSubjects");
        }
    }
}
