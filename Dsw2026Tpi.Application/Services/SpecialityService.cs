using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Application.Services
{
    public  class SpecialityService : ISpecialityService
    {
    private readonly IPersistence _persistence;
        public SpecialityService(IPersistence persistence) => _persistence = persistence;

        public async Task<Pagination<SpecialityModel.Response>> GetAll(int pageSize, int pageIndex, string? name = null)
        {
            var ex = new ValidationException();

            if (!string.IsNullOrWhiteSpace(name))
            {
                if (name.Length < 3 || name.Length > 100)
                {
                    ex.WithDetail("name", "El filtro por nombre debe tener entre 3 y 100 caracteres.");
                }
            }

            if (ex.Error.Details.Count > 0) throw ex;

            var result = await _persistence.Paginate<Speciality, string>(pageSize, pageIndex,
                s => s.IsActive && (string.IsNullOrWhiteSpace(name) || s.Name!.Contains(name)),
                s => s.Name!);

            return result.Map(s => new SpecialityModel.Response(s.Id, s.Name!, s.Description!));
        }

        public async Task<SpecialityModel.Response> Create(SpecialityModel.Request request)
        {
            Validate(request);

            var exists = await _persistence.First<Speciality>(s => s.IsActive && s.Name == request.Name);
            if (exists is not null) throw new ConflictException("SPECIALITY_CONFLICT", "Ya existe una especialidad con ese nombre.");

            var speciality = new Speciality(request.Name, request.Description);
            await _persistence.Add(speciality);
            return new SpecialityModel.Response(speciality.Id, speciality.Name, speciality.Description);
        }

        public async Task<SpecialityModel.Response> Update(Guid id, SpecialityModel.Request request)
        {
            Validate(request);
            var speciality = await _persistence.GetById<Speciality>(id) ?? throw new EntityNotFoundException("Speciality");
            speciality.Update(request.Name, request.Description);
            await _persistence.Update(speciality);
            return new SpecialityModel.Response(speciality.Id, speciality.Name, speciality.Description);
        }

        public async Task Delete(Guid id)
        {
            var speciality = await _persistence.GetById<Speciality>(id) ?? throw new EntityNotFoundException("Speciality");
            speciality.Deactivate();
            await _persistence.Update(speciality);
        }

        private static void Validate(SpecialityModel.Request request)
        {
            var ex = new ValidationException();
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length < 3 || request.Name.Length > 100)
                ex.WithDetail("name", "El nombre es obligatorio y debe tener entre 3 y 100 caracteres.");
            if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Length < 10 || request.Description.Length > 100)
                ex.WithDetail("description", "La descripción es obligatoria y debe tener entre 10 y 100 caracteres.");
            if (ex.Error.Details.Count > 0) throw ex;
        }
    }
}
