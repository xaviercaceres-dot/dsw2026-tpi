using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Application.Interfaces
{
    public  interface IAppointmentService
    {
        Task<AppointmentModel.Response> Create(AppointmentModel.CreateRequest request);
        Task<IEnumerable<AppointmentModel.Response>> GetByPatient(long dni);
        Task Cancel(Guid id);
        Task<Pagination<AppointmentModel.SearchItem>> GetByDate(DateOnly date, int pageSize, int pageIndex);
        Task<Pagination<AppointmentModel.SearchItem>> Search(Guid? specialtyId, Guid? doctorId, long? dni,
            DateOnly? date, int pageSize, int pageIndex);
    }
}
