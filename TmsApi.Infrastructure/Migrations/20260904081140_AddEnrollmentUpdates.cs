using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TMSAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollmentUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Enrollments_StudentId",
                table: "Enrollments");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:public.enrollment_status", "Approved,Completed,Pending,Rejected")
                .OldAnnotation("Npgsql:Enum:public.enrollment_status", "Approved,Pending,Rejected");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_StudentId_CourseId",
                table: "Enrollments",
                columns: new[] { "StudentId", "CourseId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Enrollments_StudentId_CourseId",
                table: "Enrollments");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:public.enrollment_status", "Approved,Pending,Rejected")
                .OldAnnotation("Npgsql:Enum:public.enrollment_status", "Approved,Completed,Pending,Rejected");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_StudentId",
                table: "Enrollments",
                column: "StudentId");
        }
    }
}
