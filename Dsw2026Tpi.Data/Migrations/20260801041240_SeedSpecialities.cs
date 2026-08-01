using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Dsw2026Tpi.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedSpecialities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Specialities",
                keyColumn: "Id",
                keyValue: new Guid("86d2451f-52f9-4921-a8e2-bd33625c0502"));

            migrationBuilder.DeleteData(
                table: "Specialities",
                keyColumn: "Id",
                keyValue: new Guid("90a96bf9-a17c-4486-8946-86f20c491412"));

            migrationBuilder.InsertData(
                table: "Specialities",
                columns: new[] { "Id", "CreatedAt", "Deleted", "Description", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("d12a12d1-1234-4321-abcd-1234567890ab"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, "Atención dental", "Odontología", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { new Guid("f23b23f2-5678-8765-bcde-2345678901bc"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, "Atención infantil", "Pediatría", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Specialities",
                keyColumn: "Id",
                keyValue: new Guid("d12a12d1-1234-4321-abcd-1234567890ab"));

            migrationBuilder.DeleteData(
                table: "Specialities",
                keyColumn: "Id",
                keyValue: new Guid("f23b23f2-5678-8765-bcde-2345678901bc"));

            migrationBuilder.InsertData(
                table: "Specialities",
                columns: new[] { "Id", "CreatedAt", "Deleted", "Description", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("86d2451f-52f9-4921-a8e2-bd33625c0502"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, "Atención dental", "Odontología", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { new Guid("90a96bf9-a17c-4486-8946-86f20c491412"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, "Atención infantil", "Pediatría", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) }
                });
        }
    }
}
