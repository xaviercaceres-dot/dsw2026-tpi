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
    private static readonly string[] DaysOrder =
        ["LUNES", "MARTES", "MIÉRCOLES", "JUEVES", "VIERNES", "SÁBADO", "DOMINGO"];

    private readonly IPersistence _persistence;

    public DoctorService(IPersistence persistence)
    {
        _persistence = persistence;
    }

    public async Task<DoctorModel.Response> CreateAsync(DoctorModel.Request request)
    {
        ValidateRequest(request);

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

        return ToResponse(doctor);
    }

    public async Task DeleteAsync(Guid id)
    {
        var existingDoctor = await _persistence.GetById<Doctor>(id);
        if (existingDoctor == null || !existingDoctor.IsActive)
        {
            throw new EntityNotFoundException("Doctor");
        }

        existingDoctor.Deactivate();

        await _persistence.Update(existingDoctor);
    }

    public async Task<Pagination<DoctorModel.Response>> GetAll(int pageSize, int pageIndex, string? name = null)
    {
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

        return doctors.Map(d=> ToResponse(d));
    }

    public async Task<IEnumerable<DoctorModel.AvailabilityDto>> GetAvailabilitiesAsync(Guid doctorId)
    {
        _ = await _persistence.GetById<Doctor>(doctorId) ?? throw new EntityNotFoundException("Doctor");

        var today = DateTime.Today;
        var rules = await _persistence.GetFiltered<AvailabilityRule>(
            r => r.DoctorId == doctorId && r.Month == today.Month && r.Year == today.Year);

        if (rules is null || !rules.Any()) return [];

        return rules
            .OrderBy(r => Array.IndexOf(DaysOrder, r.DayOfWeek))
            .Select(r => new DoctorModel.AvailabilityDto(
                r.Id,
                r.DayOfWeek!,
                Format(r.StartTime),
                Format(r.EndTime)
            ));
    }

    public async Task<DoctorModel.Response?> GetByIdAsync(Guid id)
    {
        var doctor = await _persistence.GetById<Doctor>(id, nameof(Doctor.Speciality));
        if (doctor == null || !doctor.IsActive) 
        {
            return null;
        }
        return ToResponse(doctor);
    }

    public async Task<DoctorModel.Response> UpdateAsync(Guid id, DoctorModel.Request request)
    {
        ValidateRequest(request);

        var existingDoctor = await _persistence.GetById<Doctor>(id, nameof(Doctor.Speciality));
        if (existingDoctor == null || !existingDoctor.IsActive)
        {
            throw new EntityNotFoundException("Doctor");
        }

        Speciality? speciality = null;
        if (request.SpecialityId != Guid.Empty)
        {
            speciality = await _persistence.GetById<Speciality>(request.SpecialityId);
            if (speciality == null)
            {
                throw new EntityNotFoundException("Speciality");
            }
        }

        existingDoctor.Update(request.Name, request.LicenseNumber, speciality!); 

        await _persistence.Update(existingDoctor);

        return new DoctorModel.Response(
           existingDoctor.Id,
           existingDoctor.Name,
           existingDoctor.LicenseNumber,
           existingDoctor.Speciality != null ? new DoctorModel.SpecialtyDto(existingDoctor.Speciality.Id, existingDoctor.Speciality.Name) : null
       );
    }

    private static string Format(TimeSpan time) => time.ToString(@"hh\:mm");

    private static DoctorModel.Response ToResponse(Doctor d) =>
        new(d.Id, d.Name, d.LicenseNumber, new DoctorModel.SpecialtyDto(d.Speciality?.Id, d.Speciality?.Name));

    private static void ValidateRequest(DoctorModel.Request request)
    {
        var ex = new ValidationException();

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length < 3 || request.Name.Length > 100)
            ex.WithDetail("name", "El nombre es obligatorio y debe tener entre 3 y 100 caracteres.");

        if (string.IsNullOrWhiteSpace(request.LicenseNumber))
            ex.WithDetail("licenseNumber", "El número de matrícula es obligatorio.");

        if (request.SpecialityId == Guid.Empty)
            ex.WithDetail("specialtyId", "La especialidad es obligatoria.");

        if (ex.Error.Details.Count > 0) throw ex;
    }
}
