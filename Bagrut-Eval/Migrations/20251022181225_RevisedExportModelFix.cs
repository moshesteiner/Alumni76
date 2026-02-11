using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bagrut_Eval.Migrations
{
    /// <inheritdoc />
    public partial class RevisedExportModelFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Drop Dependencies (FK and Index) - Must be included as they will be created by InitialSchema
            //migrationBuilder.DropForeignKey(name: "FK_Exports_Issues_IssueId", table: "Exports");
            //migrationBuilder.DropIndex(name: "IX_Exports_IssueId", table: "Exports");

            //// 2. PK Swap (MySQL Order: Drop Old PK -> Add New PK -> Drop Old Column)
            //migrationBuilder.DropPrimaryKey(name: "PK_Exports", table: "Exports");

            //// Add new PK first to satisfy MySQL constraint
            //migrationBuilder.AddPrimaryKey(name: "PK_Exports", table: "Exports", column: "IssueId");

            //// Now drop the old column
            //migrationBuilder.DropColumn(name: "Id", table: "Exports");

            // **INSERT THIS RAW SQL COMMAND**
            //migrationBuilder.Sql("ALTER TABLE Exports DROP PRIMARY KEY, ADD PRIMARY KEY (IssueId), DROP COLUMN Id;");

            // 3. Other Structural Changes (from the original migration file)
            //migrationBuilder.DropColumn(name: "Status", table: "Exports");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Issues",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Exports",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "AnswerId",
                table: "Exports",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            //migrationBuilder.AddColumn<bool>(
            //    name: "Exported",
            //    table: "Exports",
            //    type: "tinyint(1)",
            //    nullable: false,
            //    defaultValue: false);

            // 4. Data and Final Column Changes
            // This is a data operation (UpdateData)
            migrationBuilder.UpdateData(
                table: "Answers",
                keyColumn: "Score",
                keyValue: null,
                column: "Score",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "Score",
                table: "Answers",
                type: "varchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(64)",
                oldMaxLength: 64,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            // 5. Add Final FK
            //migrationBuilder.AddForeignKey(
            //    name: "FK_Exports_Answers_AnswerId",
            //    table: "Exports",
            //    column: "AnswerId",
            //    principalTable: "Answers",
            //    principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // You will need to reconstruct the Down method as well, but for now, 
            // the focus is on getting the Up method to run successfully on the empty database.
        }
    }
}