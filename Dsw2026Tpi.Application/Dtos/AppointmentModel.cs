using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Dsw2026Tpi.Application.Dtos
{
    public record  AppointmentModel
    {
        public record PatientRequest(long Dni);
        public record CreateRequest(Guid DoctorId, [property: JsonPropertyName("availabilitySlotId")] Guid AvailabilityId, PatientRequest Patient, string Reason);

        public record Response(
            Guid Id, Guid DoctorId, string DoctorName, string Speciality,
            long PatientDni, string PatientName, DateOnly Date,
            string StartTime, string EndTime, string Reason, string Status);

        public record PatientSummary(long Dni, string FullName);
        public record SpecialtySummary(Guid SpecialtyId, string Name);
        public record DoctorSummary(Guid DoctorId, string Name, SpecialtySummary Specialty);
        public record SearchItem(Guid AppointmentsId, string AppointmentsStatus, PatientSummary Patient, DoctorSummary Doctor);
    }
}
