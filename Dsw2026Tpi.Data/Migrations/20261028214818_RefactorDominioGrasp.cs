using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dsw2026Tpi.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefactorDominioGrasp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AvailabilitySlots_Doctors_DoctorId",
                table: "AvailabilitySlots");

            migrationBuilder.DropIndex(
                name: "IX_AvailabilitySlots_AvailabilityRuleId",
                table: "AvailabilitySlots");

            migrationBuilder.DropIndex(
                name: "IX_AvailabilitySlots_DoctorId_SlotDate_StartTime",
                table: "AvailabilitySlots");

            migrationBuilder.DropColumn(
                name: "DoctorId",
                table: "AvailabilitySlots");

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilitySlots_AvailabilityRuleId_SlotDate_StartTime",
                table: "AvailabilitySlots",
                columns: new[] { "AvailabilityRuleId", "SlotDate", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_AvailabilitySlotId",
                table: "Appointments",
                column: "AvailabilitySlotId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AvailabilitySlots_AvailabilityRuleId_SlotDate_StartTime",
                table: "AvailabilitySlots");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_AvailabilitySlotId",
                table: "Appointments");

            migrationBuilder.AddColumn<Guid>(
                name: "DoctorId",
                table: "AvailabilitySlots",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilitySlots_AvailabilityRuleId",
                table: "AvailabilitySlots",
                column: "AvailabilityRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilitySlots_DoctorId_SlotDate_StartTime",
                table: "AvailabilitySlots",
                columns: new[] { "DoctorId", "SlotDate", "StartTime" });

            migrationBuilder.AddForeignKey(
                name: "FK_AvailabilitySlots_Doctors_DoctorId",
                table: "AvailabilitySlots",
                column: "DoctorId",
                principalTable: "Doctors",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
