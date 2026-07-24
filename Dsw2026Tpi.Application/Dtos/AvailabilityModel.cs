using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Application.Dtos
{
    public record AvailabilityModel
    {
        public record DayScheduleDto(string Day, string StartTime, string EndTime);
        public record Request(Guid DoctorId, List<DayScheduleDto> Days);

    }
}
