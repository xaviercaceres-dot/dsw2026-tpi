using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.CrossCutting.Resources;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Enums;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Dsw2026Tpi.Application.Services;

/// <summary>
/// Controlador de los casos de uso CU01 - Reservar Turno, CU03 - Cancelar Turno y de las búsquedas de citas.
/// Las reglas de reserva y cancelación pertenecen al dominio (AvailabilitySlot / Appointment);
/// este servicio valida la entrada, obtiene los objetos, les delega la operación y persiste.
/// </summary>
public class AppointmentService : IAppointmentService
{
    private const int DefaultPageSize = 10;
    private const int ReasonMinLength = 5;
    private const int ReasonMaxLength = 300; // appointments.reason varchar(300)

    private static readonly string[] AppointmentDetailIncludes =
    [
        $"{nameof(Appointment.AvailabilitySlot)}.{nameof(AvailabilitySlot.AvailabilityRule)}.{nameof(AvailabilityRule.Doctor)}.{nameof(Doctor.Speciality)}",
        nameof(Appointment.Patient)
    ];

    private readonly IPersistence _persistence;
    private readonly IPatientService _patientService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AppointmentService> _logger;

    public AppointmentService(IPersistence persistence, IPatientService patientService,
        TimeProvider timeProvider, ILogger<AppointmentService> logger)
    {
        _persistence = persistence;
        _patientService = patientService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<AppointmentModel.Response> Create(AppointmentModel.CreateRequest request, string userId)
    {
        Validate(request);
        var patient = await GetAuthenticatedPatient(userId, request.Patient.Dni);

        var doctor = await _persistence.GetById<Doctor>(request.DoctorId, nameof(Doctor.Speciality));
        if (doctor is null || !doctor.IsActive)
            throw new EntityNotFoundException(nameof(Doctor));

        var slot = await _persistence.GetById<AvailabilitySlot>(request.AvailabilityId, nameof(AvailabilitySlot.AvailabilityRule))
            ?? throw new EntityNotFoundException(nameof(AvailabilitySlot));

        if (!slot.BelongsTo(doctor.Id))
            throw new ValidationException().WithDetail("availabilitySlotId", "El turno no pertenece al médico indicado.");

        Appointment appointment;
        try
        {
            appointment = slot.Book(patient.Id, request.Reason.Trim(), Now());
        }
        catch (AppException ex)
        {
            _logger.LogWarning("Reserva rechazada del turno {SlotId} (doctor {DoctorId}): {ErrorCode}",
                slot.Id, doctor.Id, ex.Error.ErrorCode);
            throw;
        }

        try
        {
            // Un único guardado: inserta la cita y actualiza el estado del turno (control de concurrencia por RowVersion).
            await _persistence.Add(appointment);
        }
        catch (ConflictException ex) when (ex.Error.ErrorCode == nameof(ErrorCodes.CONCURRENCY_CONFLICT))
        {
            _logger.LogWarning("Conflicto de concurrencia al reservar el turno {SlotId}", slot.Id);
            throw new ConflictException("APPOINTMENT_CONFLICT", "Slot already booked")
                .WithDetail("availabilitySlotId", "slot_unavailable");
        }

        _logger.LogInformation("Turno {AppointmentId} reservado para paciente {PatientId} con doctor {DoctorId}",
            appointment.Id, patient.Id, doctor.Id);

        return ToResponse(appointment, slot, doctor, patient);
    }

    public async Task<IEnumerable<AppointmentModel.Response>> GetByPatient(long dni, string userId)
    {
        if (dni <= 0 || dni.ToString().Length is < 7 or > 10)
            throw new ValidationException().WithDetail("dni", "El DNI es obligatorio y debe tener entre 7 y 10 dígitos.");

        var patient = await GetAuthenticatedPatient(userId, dni);

        var appointments = await _persistence.GetFiltered<Appointment>(
            a => a.PatientId == patient.Id && a.Status == AppointmentStatus.BOOKED,
            AppointmentDetailIncludes) ?? [];

        return appointments
            .OrderBy(a => a.AvailabilitySlot!.StartsAt)
            .Select(a => ToResponse(a, a.AvailabilitySlot!, a.AvailabilitySlot!.AvailabilityRule!.Doctor!, a.Patient!))
            .ToList();
    }

    public async Task Cancel(Guid id, string userId)
    {
        var patient = await GetAuthenticatedPatient(userId);
        var appointment = await _persistence.GetById<Appointment>(id, nameof(Appointment.AvailabilitySlot))
            ?? throw new EntityNotFoundException(nameof(Appointment));

        if (appointment.PatientId != patient.Id)
        {
            _logger.LogWarning("Paciente {PatientId} intentó cancelar la cita {AppointmentId} de otro paciente", patient.Id, id);
            throw new AuthorizationException();
        }

        try
        {
            appointment.Cancel();
        }
        catch (AppException ex)
        {
            _logger.LogWarning("Cancelación rechazada del turno {AppointmentId} en estado {Status}: {ErrorCode}",
                appointment.Id, appointment.Status, ex.Error.ErrorCode);
            throw;
        }

        // Un único guardado: la cita cancelada y su turno nuevamente disponible.
        await _persistence.Update(appointment);

        _logger.LogInformation("Turno {AppointmentId} cancelado; turno {SlotId} liberado",
            appointment.Id, appointment.AvailabilitySlotId);
    }

    public Task<Pagination<AppointmentModel.SearchItem>> GetByDate(DateOnly date, int pageSize, int pageIndex)
    {
        if (date == default)
            throw new ValidationException().WithDetail("date", "La fecha es obligatoria (formato YYYY-MM-DD).");

        return Search(null, null, null, date, pageSize, pageIndex);
    }

    public async Task<Pagination<AppointmentModel.SearchItem>> Search(Guid? specialtyId, Guid? doctorId, long? dni,
        DateOnly? date, int pageSize, int pageIndex)
    {
        if (pageSize <= 0) pageSize = DefaultPageSize;
        if (pageIndex <= 0) pageIndex = 1;

        var result = await _persistence.Paginate<Appointment, DateTime>(pageSize, pageIndex,
            BuildSearchFilter(specialtyId, doctorId, dni, date),
            a => a.AvailabilitySlot!.SlotDate,
            AppointmentDetailIncludes);

        return result.Map(ToSearchItem);
    }

    private DateTime Now() => _timeProvider.GetLocalNow().DateTime;

    /// <summary>
    /// Paciente del usuario autenticado. Un paciente solo puede operar con su propio DNI y sus propias citas.
    /// </summary>
    private async Task<Patient> GetAuthenticatedPatient(string userId, long? dni = null)
    {
        var patient = await _patientService.GetByUserIdAsync(userId);
        if (patient is null || (dni.HasValue && patient.Dni != dni.Value))
        {
            _logger.LogWarning("Acceso denegado: el usuario {UserId} intentó operar con un paciente que no le corresponde", userId);
            throw new AuthorizationException();
        }
        return patient;
    }

    private static Expression<Func<Appointment, bool>> BuildSearchFilter(Guid? specialtyId, Guid? doctorId, long? dni, DateOnly? date)
    {
        var day = date?.ToDateTime(TimeOnly.MinValue);

        return a =>
            (!specialtyId.HasValue || a.AvailabilitySlot!.AvailabilityRule!.Doctor!.SpecialityId == specialtyId) &&
            (!doctorId.HasValue || a.AvailabilitySlot!.AvailabilityRule!.DoctorId == doctorId) &&
            (!dni.HasValue || a.Patient!.Dni == dni) &&
            (!day.HasValue || a.AvailabilitySlot!.SlotDate == day.Value);
    }

    private static void Validate(AppointmentModel.CreateRequest? request)
    {
        if (request is null)
            throw new ValidationException("El cuerpo de la solicitud es obligatorio.", "APPOINTMENT_REQUEST_REQUIRED");

        var ex = new ValidationException();

        if (request.DoctorId == Guid.Empty)
            ex.WithDetail("doctorId", "El médico es obligatorio.");

        if (request.AvailabilityId == Guid.Empty)
            ex.WithDetail("availabilitySlotId", "El turno es obligatorio.");

        if (request.Patient is null || request.Patient.Dni <= 0 || request.Patient.Dni.ToString().Length is < 7 or > 10)
            ex.WithDetail("patient.dni", "El DNI es obligatorio y debe tener entre 7 y 10 dígitos.");

        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length < ReasonMinLength)
            ex.WithDetail("reason", $"El motivo es obligatorio (mínimo {ReasonMinLength} caracteres).");
        else if (reason.Length > ReasonMaxLength)
            ex.WithDetail("reason", $"El motivo no puede superar los {ReasonMaxLength} caracteres.");

        if (ex.Error.Details.Count > 0) throw ex;
    }

    private static AppointmentModel.Response ToResponse(Appointment a, AvailabilitySlot slot, Doctor doctor, Patient patient) =>
        new(a.Id, doctor.Id, doctor.Name, doctor.Speciality?.Name ?? string.Empty,
            patient.Dni, patient.FullName ?? string.Empty, DateOnly.FromDateTime(slot.SlotDate),
            slot.StartTime.ToString(@"hh\:mm"), slot.EndTime.ToString(@"hh\:mm"), a.Reason, a.Status.ToString());

    private static AppointmentModel.SearchItem ToSearchItem(Appointment a)
    {
        var doctor = a.AvailabilitySlot!.AvailabilityRule!.Doctor!;
        return new(a.Id, a.Status.ToString(),
            new AppointmentModel.PatientSummary(a.Patient!.Dni, a.Patient.FullName ?? string.Empty),
            new AppointmentModel.DoctorSummary(doctor.Id, doctor.Name,
                new AppointmentModel.SpecialtySummary(doctor.Speciality?.Id ?? Guid.Empty, doctor.Speciality?.Name ?? string.Empty)),
            a.AvailabilitySlot.StartsAt);
    }
}
