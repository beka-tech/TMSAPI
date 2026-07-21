using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TMSAPI.Migrations;

/// <inheritdoc />
public partial class AddStudentLastUpdated : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "LastUpdated",
            table: "Students",
            type: "timestamp without time zone",
            nullable: false,
            defaultValueSql: "CURRENT_TIMESTAMP"
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "LastUpdated", table: "Students");
    }
}
