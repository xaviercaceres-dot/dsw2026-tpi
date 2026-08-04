using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Application.Dtos
{
    public record  AppointmentModel
    {
        public record PatientRequest(long Dni);
        public record CreateRequest(Guid DoctorId, Guid AvailabilityId, PatientRequest Patient, string Reason);

        public record Response(
            Guid Id, Guid DoctorId, string DoctorName, string Speciality,
            long PatientDni, string PatientName, DateOnly Date,
            string StartTime, string EndTime, string Reason, string Status);
    }
}
