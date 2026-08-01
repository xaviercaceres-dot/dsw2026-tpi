using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;

namespace Dsw2026Tpi.Application.Services;
//CORREGIR 
public class DoctorService : IDoctorService
{
    private static readonly string[] DaysOrder =
        ["LUNES", "MARTES", "MIÉRCOLES", "JUEVES", "VIERNES", "SÁBADO", "DOMINGO"];

    private readonly IPersistence _persistence;

    public DoctorService(IPersistence persistence)
    {
        _persistence = persistence;
    }

    public async Task<Pagination<DoctorModel.Response>> GetAll(int pageSize, int pageIndex, string? name = null)
    {
        var doctors = await _persistence.Paginate<Doctor, string>(pageSize, pageIndex, d => d.IsActive && (string.IsNullOrWhiteSpace(name) ||
                                                   d.Name.Contains(name)), x => x.Name, nameof(Doctor.Speciality));

        return doctors.Map(d => new DoctorModel.Response(d.Id, d.Name, d.LicenseNumber,
            new DoctorModel.SpecialtyDto(d.Speciality?.Id, d.Speciality?.Name)));
    }


    public async Task<DoctorModel.Response> Create(DoctorModel.Request request)
    {
        ValidateRequest(request);

        var speciality = await _persistence.GetById<Speciality>(request.SpecialityId)
            ?? throw new EntityNotFoundException("Speciality");

        var doctor = new Doctor(request.Name, request.LicenseNumber, speciality);
        await _persistence.Add(doctor);

        return ToResponse(doctor);
    }

    public async Task<DoctorModel.Response> Update(Guid id, DoctorModel.Request request)
    {
        ValidateRequest(request);

        var doctor = await _persistence.GetById<Doctor>(id, nameof(Doctor.Speciality))
            ?? throw new EntityNotFoundException("Doctor");

        var speciality = await _persistence.GetById<Speciality>(request.SpecialityId)
            ?? throw new EntityNotFoundException("Specialty");

        doctor.Update(request.Name, request.LicenseNumber, speciality);
        await _persistence.Update(doctor);

        return ToResponse(doctor);
    }

    public async Task Delete(Guid id)
    {
        var doctor = await _persistence.GetById<Doctor>(id) ?? throw new EntityNotFoundException("Doctor");
        doctor.Deactivate();
        await _persistence.Update(doctor);
    }

    public async Task<IEnumerable<DoctorModel.AvailabilityDto>> GetAvailabilities(Guid doctorId)
    {
        _ = await _persistence.GetById<Doctor>(doctorId) ?? throw new EntityNotFoundException("Doctor");

        var today = DateTime.Today;
        var rules = await _persistence.GetFiltered<AvailabilityRule>(
            r => r.DoctorId == doctorId && r.Month == today.Month && r.Year == today.Year);

        if (rules is null || !rules.Any()) return [];

        return rules
            .OrderBy(r => Array.IndexOf(DaysOrder, r.DayOfWeek))
            .Select(r => new DoctorModel.AvailabilityDto(r.DayOfWeek, Format(r.StartTime), Format(r.EndTime)));
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
