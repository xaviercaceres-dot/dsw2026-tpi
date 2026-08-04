using Dsw2026Tpi.Application.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Application.Interfaces
{
    
        public interface IAvailabilityService
        {
        Task<IEnumerable<AvailabilityModel.RuleDto>> Create(AvailabilityModel.Request request);
        Task<IEnumerable<AvailabilityModel.RuleDto>> Update(AvailabilityModel.Request request);
        Task<IEnumerable<AvailabilityModel.SlotDto>> GetAvailableSlots(Guid doctorId, DateOnly date);
   
        }

    
}
