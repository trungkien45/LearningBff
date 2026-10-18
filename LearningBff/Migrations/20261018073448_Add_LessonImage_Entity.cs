using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LearningBff.Migrations
{
    /// <inheritdoc />
    public partial class Add_LessonImage_Entity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppLessonImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<long>(type: "bigint", nullable: true),
                    ChapterId = table.Column<long>(type: "bigint", nullable: true),
                    BlobName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLessonImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppLessonImages_AppChapters_ChapterId",
                        column: x => x.ChapterId,
                        principalTable: "AppChapters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppLessonImages_AppLessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "AppLessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppLessonImages_ChapterId",
                table: "AppLessonImages",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLessonImages_LessonId",
                table: "AppLessonImages",
                column: "LessonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppLessonImages");
        }
    }
}
