using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.CrossCutting.Resources;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using System.Linq.Expressions;

namespace Dsw2026Tpi.Application.Services;

public class DoctorService : IDoctorService
{
    private readonly IPersistence _persistence;

    public DoctorService(IPersistence persistence)
    {
        _persistence = persistence;
    }

    public async Task<DoctorModel.Response> CreateAsync(DoctorModel.Request request)
    {
        ValidateDoctorRequest(request);

        if (request.SpecialityId == Guid.Empty)
        {
            throw new ValidationException(ErrorCodes.DOCTOR_SPECIALITY_ID_REQUIRED, "Speciality ID is required.");
        }

        var speciality = await _persistence.GetById<Speciality>(request.SpecialityId);

        if(speciality == null)
        {
            throw new ValidationException(ErrorCodes.SPECIALITY_NOT_FOUND, "Speciality not found.");
        }

        var doctor = new Doctor(request.Name, request.LicenseNumber, speciality);

        await _persistence.Add(doctor);

        return new DoctorModel.Response(
            doctor.Id,
            doctor.Name,
            doctor.LicenseNumber,
            speciality != null ? new DoctorModel.SpecialityDto(speciality.Id, speciality.Name) : null
        );
    }

    public async Task DeleteAsync(Guid id)
    {
        var existingDoctor = await _persistence.GetById<Doctor>(id);
        if (existingDoctor == null || !existingDoctor.IsActive)
        {
            //throw new ValidationException(ErrorCodes.ENTITY_NOTFOUND, "Doctor not found or inactive.");
            throw new EntityNotFoundException("Doctor");
        }

        existingDoctor.Deactivate();

        await _persistence.Update(existingDoctor);
    }

    public async Task<Pagination<DoctorModel.Response>> GetAll(int pageSize, int pageIndex, string? name = null)
    {
        //var doctors = await _persistence.Paginate<Doctor, string>(pageSize, pageIndex, d => string.IsNullOrWhiteSpace(name) ||
        //                                           d.Name.Contains(name), x => x.Name, nameof(Doctor.Speciality));

        //return doctors.Map(d => new DoctorModel.Response(d.Id, d.Name, d.LicenseNumber,
        //    new DoctorModel.SpecialityDto(d.Speciality?.Id, d.Speciality?.Name)));

        if (pageSize <= 0) pageSize = 10; 
        if (pageIndex < 0) pageIndex = 0; 

        Expression<Func<Doctor, bool>> filterPredicate = d =>
            d.IsActive && 
            (string.IsNullOrWhiteSpace(name) || d.Name.Contains(name));

        var doctors = await _persistence.Paginate<Doctor, string>(
            pageSize, pageIndex,
            filterPredicate,
            x => x.Name, 
            nameof(Doctor.Speciality));

        return doctors.Map(d => new DoctorModel.Response(
            d.Id,
            d.Name,
            d.LicenseNumber,
            d.Speciality != null ? new DoctorModel.SpecialityDto(d.Speciality.Id, d.Speciality.Name) : null
        ));
    }

    public async Task<DoctorModel.Response?> GetByIdAsync(Guid id)
    {
        var doctor = await _persistence.GetById<Doctor>(id, nameof(Doctor.Speciality));
        if (doctor == null || !doctor.IsActive) 
        {
            return null;
        }
        return new DoctorModel.Response(
            doctor.Id,
            doctor.Name,
            doctor.LicenseNumber,
            doctor.Speciality != null ? new DoctorModel.SpecialityDto(doctor.Speciality.Id, doctor.Speciality.Name) : null
        );
    }

    public async Task<DoctorModel.Response> UpdateAsync(Guid id, DoctorModel.Request request)
    {
        ValidateDoctorRequest(request);

        var existingDoctor = await _persistence.GetById<Doctor>(id, nameof(Doctor.Speciality));
        if (existingDoctor == null || !existingDoctor.IsActive)
        {
            //throw new ValidationException(ErrorCodes.ENTITY_NOTFOUND, "Doctor not found or inactive.");
            throw new EntityNotFoundException("Doctor");
        }

        Speciality? speciality = null;
        if (request.SpecialityId != Guid.Empty)
        {
            speciality = await _persistence.GetById<Speciality>(request.SpecialityId);
            if (speciality == null)
            {
                //throw new ValidationException(ErrorCodes.ENTITY_NOTFOUND, "Speciality not found.");
                throw new EntityNotFoundException("Speciality");
            }
        }

        existingDoctor.UpdateDetails(request.Name, request.LicenseNumber, speciality); 

        await _persistence.Update(existingDoctor);

        return new DoctorModel.Response(
           existingDoctor.Id,
           existingDoctor.Name,
           existingDoctor.LicenseNumber,
           existingDoctor.Speciality != null ? new DoctorModel.SpecialityDto(existingDoctor.Speciality.Id, existingDoctor.Speciality.Name) : null
       );
    }

    private static void ValidateDoctorRequest(DoctorModel.Request request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException(ErrorCodes.DOCTOR_NAME_REQUIRED, "Doctor name is required.");

        if (request.Name.Length < 3 || request.Name.Length > 100)
            throw new ValidationException(ErrorCodes.DOCTOR_NAME_LENGTH_INVALID, "Doctor name must be between 3 and 100 characters.");

        if (string.IsNullOrWhiteSpace(request.LicenseNumber))
            throw new ValidationException(ErrorCodes.DOCTOR_LICENSE_NUMBER_REQUIRED, "Doctor license number is required.");

        if (request.LicenseNumber.Length > 50)
            throw new ValidationException(ErrorCodes.DOCTOR_LICENSE_NUMBER_LENGTH_INVALID, "Doctor license number must be at most 50 characters.");
    }
}
