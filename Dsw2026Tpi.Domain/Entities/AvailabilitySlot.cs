using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Domain.Entities
{
    public class AvailabilitySlot : EntityBase

    {
        public Guid AvailabilityRuleId { get; init; }
        public AvailabilityRule? AvailabilityRule { get; private set; }
        public Guid DoctorId { get; init; }

        public Doctor? Doctor { get; init; }
        public DateTime SlotDate { get; init; }
        public TimeSpan StartTime { get; init; }
        public TimeSpan EndTime { get; init; }
        public string Status { get; private set; }

#pragma warning disable CS8618
        private AvailabilitySlot() { }
#pragma warning restore CS8618

        public AvailabilitySlot(Guid availabilityRuleId, Guid doctorId, DateTime slotDate, TimeSpan startTime, TimeSpan endTime, Guid? id = null) : base(id)
        {
            AvailabilityRuleId = availabilityRuleId;
            DoctorId = doctorId;
            SlotDate = slotDate;
            StartTime = startTime;
            EndTime = endTime;
            Status = "AVAILABLE";
        }

        public void Block() => Status = "BLOCKED";
        public void Book() => Status = "BOOKED";
        public void MakeAvailable() => Status = "AVAILABLE";

    }
}
