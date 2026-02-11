using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bagrut_Eval.Migrations
{
    /// <inheritdoc />
    public partial class AddSubjectsAndExamLock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. ALTER existing columns (MetricsLog) - No dependency issue
            migrationBuilder.AlterColumn<string>(
                name: "ScoreType",
                table: "MetricsLog",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "RuleDescription",
                table: "MetricsLog",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            // 2. Add new columns to Exams
            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "Exams",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SubjectId",
                table: "Exams",
                type: "int",
                nullable: false,
                defaultValue: 0); // All existing Exams rows now have SubjectId = 0

            // 3. Create Subjects table (The principal table for the FK)
            migrationBuilder.CreateTable(
                name: "Subjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Title = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subjects", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // 4. Create UserSubjects join table
            migrationBuilder.CreateTable(
                name: "UserSubjects",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SubjectId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSubjects", x => new { x.UserId, x.SubjectId });
                    table.ForeignKey(
                        name: "FK_UserSubjects_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserSubjects_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");


            // 🛑 START OF CRITICAL DATA OPERATIONS (MOVED HERE) 🛑

            // 5. Insert Seed Data into Subjects (Creates records with Ids 1, 2, and 3)
            migrationBuilder.InsertData(
                table: "Subjects",
                columns: new[] { "Id", "Title" },
                values: new object[] { 1, "ללא מקצוע" }); // Id=1
            migrationBuilder.InsertData(
               table: "Subjects",
               columns: new[] { "Id", "Title" },
               values: new object[] { 2, "מדעי המחשב" }); // Id=2
            migrationBuilder.InsertData(
               table: "Subjects",
               columns: new[] { "Id", "Title" },
               values: new object[] { 3, "מתמטיקה" }); // Id=3

            // 6. UPDATE Exams Data (CRITICAL STEP)
            // Fix existing Exams rows by setting their SubjectId (currently 0) to a valid Id (Id=2)
            migrationBuilder.Sql("UPDATE `Exams` SET `SubjectId` = 2 WHERE `SubjectId` = 0 OR `SubjectId` IS NULL");

            // 🛑 END OF CRITICAL DATA OPERATIONS 🛑


            // 7. Create Indexes
            migrationBuilder.CreateIndex(
                name: "IX_Exams_SubjectId",
                table: "Exams",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubjects_SubjectId",
                table: "UserSubjects",
                column: "SubjectId");

            // 8. Add Foreign Key (Now that Exams.SubjectId contains only valid values, this will succeed)
            migrationBuilder.AddForeignKey(
                name: "FK_Exams_Subjects_SubjectId",
                table: "Exams",
                column: "SubjectId",
                principalTable: "Subjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ... (Down method remains the same) ...
            migrationBuilder.DropForeignKey(
                name: "FK_Exams_Subjects_SubjectId",
                table: "Exams");

            migrationBuilder.DropTable(
                name: "UserSubjects");

            migrationBuilder.DropTable(
                name: "Subjects");

            migrationBuilder.DropIndex(
                name: "IX_Exams_SubjectId",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "IsLocked",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "SubjectId",
                table: "Exams");

            // ... (Rest of the Down method for MetricsLog) ...
            migrationBuilder.UpdateData(
                table: "MetricsLog",
                keyColumn: "ScoreType",
                keyValue: null,
                column: "ScoreType",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "ScoreType",
                table: "MetricsLog",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "MetricsLog",
                keyColumn: "RuleDescription",
                keyValue: null,
                column: "RuleDescription",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "RuleDescription",
                table: "MetricsLog",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}