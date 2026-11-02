using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LearningBff.Migrations
{
    /// <inheritdoc />
    public partial class Add_Essay_And_Mixed_Exam_Support : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GradingGuide",
                table: "AppQuestions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "AppExams",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "GradedByUserId",
                table: "AppExamResults",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GradedTime",
                table: "AppExamResults",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GradingStatus",
                table: "AppExamResults",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OverallFeedback",
                table: "AppExamResults",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "AnswerId",
                table: "AppExamResultAnswers",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<string>(
                name: "AnswerText",
                table: "AppExamResultAnswers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsGraded",
                table: "AppExamResultAnswers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StudentAnswerText",
                table: "AppExamResultAnswers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeacherFeedback",
                table: "AppExamResultAnswers",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppExamResults_GradedByUserId",
                table: "AppExamResults",
                column: "GradedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppExamResults_AbpUsers_GradedByUserId",
                table: "AppExamResults",
                column: "GradedByUserId",
                principalTable: "AbpUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppExamResults_AbpUsers_GradedByUserId",
                table: "AppExamResults");

            migrationBuilder.DropIndex(
                name: "IX_AppExamResults_GradedByUserId",
                table: "AppExamResults");

            migrationBuilder.DropColumn(
                name: "GradingGuide",
                table: "AppQuestions");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "AppExams");

            migrationBuilder.DropColumn(
                name: "GradedByUserId",
                table: "AppExamResults");

            migrationBuilder.DropColumn(
                name: "GradedTime",
                table: "AppExamResults");

            migrationBuilder.DropColumn(
                name: "GradingStatus",
                table: "AppExamResults");

            migrationBuilder.DropColumn(
                name: "OverallFeedback",
                table: "AppExamResults");

            migrationBuilder.DropColumn(
                name: "AnswerText",
                table: "AppExamResultAnswers");

            migrationBuilder.DropColumn(
                name: "IsGraded",
                table: "AppExamResultAnswers");

            migrationBuilder.DropColumn(
                name: "StudentAnswerText",
                table: "AppExamResultAnswers");

            migrationBuilder.DropColumn(
                name: "TeacherFeedback",
                table: "AppExamResultAnswers");

            migrationBuilder.AlterColumn<long>(
                name: "AnswerId",
                table: "AppExamResultAnswers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
