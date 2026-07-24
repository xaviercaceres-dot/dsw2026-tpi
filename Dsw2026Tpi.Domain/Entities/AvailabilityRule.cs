using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Domain.Entities
{
   public class AvailabilityRule : EntityBase
    {
        public Guid DoctorId { get; init; }
        public Doctor? Doctor { get; init; }
        public int Month { get; init; }
        public int Year { get; init; }
        public string DayOfWeek { get; init; }
        public TimeSpan StartTime { get; init; }
        public TimeSpan EndTime { get; init; }

        

        private AvailabilityRule() { }


        public AvailabilityRule(Guid doctorId, int month, int year, string dayOfWeek, TimeSpan startTime, TimeSpan endTime, Guid? id = null) : base(id)
        {
            DoctorId = doctorId;
            Month = month;
            Year = year;
            DayOfWeek = dayOfWeek;
            StartTime = startTime;
            EndTime = endTime;
        }
    
    }
}
