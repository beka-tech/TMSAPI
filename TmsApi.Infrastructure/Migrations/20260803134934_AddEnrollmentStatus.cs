using Microsoft.EntityFrameworkCore.Migrations;
using TmsApi.Domain.Enums;

#nullable disable

namespace TMSAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollmentStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:public.enrollment_status", "Approved,Pending,Rejected");

            migrationBuilder.AddColumn<EnrollmentStatus>(
                name: "Status",
                table: "Enrollments",
                type: "public.enrollment_status",
                nullable: false,
                defaultValue: EnrollmentStatus.Pending);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Enrollments");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:Enum:public.enrollment_status", "Approved,Pending,Rejected");
        }
    }
}
