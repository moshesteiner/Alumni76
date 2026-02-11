using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bagrut_Eval.Migrations
{
    /// <inheritdoc />
    public partial class MakeIssuePartIdNullableAndAddExamQuestionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Issues_Answers_AnswerId",
                table: "Issues");

            migrationBuilder.RenameColumn(
                name: "AnswerId",
                table: "Issues",
                newName: "FinalAnswerId");

            migrationBuilder.RenameIndex(
                name: "IX_Issues_AnswerId",
                table: "Issues",
                newName: "IX_Issues_FinalAnswerId");

            migrationBuilder.AlterColumn<int>(
                name: "PartId",
                table: "Issues",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.UpdateData(
                table: "Issues",
                keyColumn: "Description",
                keyValue: null,
                column: "Description",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Issues",
                type: "varchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "ExamId",
                table: "Issues",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "QuestionNumber",
                table: "Issues",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Issues_ExamId",
                table: "Issues",
                column: "ExamId");

            migrationBuilder.AddForeignKey(
                name: "FK_Issues_Answers_FinalAnswerId",
                table: "Issues",
                column: "FinalAnswerId",
                principalTable: "Answers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Issues_Exams_ExamId",
                table: "Issues",
                column: "ExamId",
                principalTable: "Exams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Issues_Answers_FinalAnswerId",
                table: "Issues");

            migrationBuilder.DropForeignKey(
                name: "FK_Issues_Exams_ExamId",
                table: "Issues");

            migrationBuilder.DropIndex(
                name: "IX_Issues_ExamId",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "ExamId",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "QuestionNumber",
                table: "Issues");

            migrationBuilder.RenameColumn(
                name: "FinalAnswerId",
                table: "Issues",
                newName: "AnswerId");

            migrationBuilder.RenameIndex(
                name: "IX_Issues_FinalAnswerId",
                table: "Issues",
                newName: "IX_Issues_AnswerId");

            migrationBuilder.AlterColumn<int>(
                name: "PartId",
                table: "Issues",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Issues",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(500)",
                oldMaxLength: 500)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddForeignKey(
                name: "FK_Issues_Answers_AnswerId",
                table: "Issues",
                column: "AnswerId",
                principalTable: "Answers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
