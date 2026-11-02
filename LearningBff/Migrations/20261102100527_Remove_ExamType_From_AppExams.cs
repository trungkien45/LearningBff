using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LearningBff.Migrations
{
    /// <inheritdoc />
    public partial class Remove_ExamType_From_AppExams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Type",
                table: "AppExams");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "AppExams",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
