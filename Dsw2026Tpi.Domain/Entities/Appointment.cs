using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Domain.Entities
{
    public class Appointment : EntityBase
    {
        public Guid AvailabilitySlotId { get; init; }
        public AvailabilitySlot? AvailabilitySlot { get; init; }
        public Guid PatientId { get; init; }
        public Patient? Patient { get; init; }
        public string Reason { get; private set; }
        public string Status { get; private set; }
        public DateTime? CancelledAt { get; private set; }
        public DateTime? AttendedAt { get; private set; }

#pragma warning disable CS8618
        private Appointment() { }
#pragma warning restore CS8618

        public Appointment(Guid availabilitySlotId, Guid patientId, string reason, Guid? id = null) : base(id)
        {
            AvailabilitySlotId = availabilitySlotId;
            PatientId = patientId;
            Reason = reason;
            Status = "BOOKED";
        }

        public void Cancel()
        {
            Status = "CANCELLED";
            CancelledAt = DateTime.UtcNow;
        }

        public void Attend()
        {
            Status = "ATTENDED";
            AttendedAt = DateTime.UtcNow;
        }

        public void MarkNoShow() => Status = "NO_SHOW";
    }
}
