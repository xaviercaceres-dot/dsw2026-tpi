using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dsw2026Tpi.Data.Migrations
{
    public partial class ParcheColumnaDeleted : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Specialities', 'deleted') IS NULL
                    ALTER TABLE Specialities ADD deleted bit NOT NULL DEFAULT 0;
                
                IF COL_LENGTH('Doctors', 'deleted') IS NULL
                    ALTER TABLE Doctors ADD deleted bit NOT NULL DEFAULT 0;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}