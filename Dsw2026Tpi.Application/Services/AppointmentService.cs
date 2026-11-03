using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Enums;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dsw2026Tpi.Application.Services;

public class AppointmentService : IAppointmentService
{
    private const string SlotIncludes1 = $"{nameof(AvailabilitySlot)}.{nameof(AvailabilityRule)}.{nameof(Doctor)}.{nameof(Doctor.Speciality)}";
    private const string SlotIncludes2 = nameof(Patient);

    private readonly IPersistence _persistence;
    private readonly IPatientService _patientService;
    private readonly ILogger<AppointmentService> _logger;

    public AppointmentService(IPersistence persistence, IPatientService patientService, ILogger<AppointmentService> logger)
    {
        _persistence = persistence;
        _patientService = patientService;
        _logger = logger;
    }

    public async Task<AppointmentModel.Response> Create(AppointmentModel.CreateRequest request)
    {
        var validation = new ValidationException();
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length < 5 || request.Reason.Length > 300)
            validation.WithDetail("reason", "El motivo es obligatorio y debe tener entre 5 y 300 caracteres.");
        if (request.Patient is null || request.Patient.Dni < 1_000_000 || request.Patient.Dni > 99_999_999)
            validation.WithDetail("dni", "El DNI es obligatorio y debe tener 7 u 8 dígitos.");
        if (request.DoctorId == Guid.Empty)
            validation.WithDetail("doctorId", "El médico es obligatorio.");
        if (request.AvailabilityId == Guid.Empty)
            validation.WithDetail("availabilitySlotId", "El turno es obligatorio.");
        if (validation.Error.Details.Count > 0)
            throw validation;

        var patient = await _patientService.GetByDniAsync(request.Patient!.Dni)
            ?? throw new EntityNotFoundException("Patient");
        var slot = await _persistence.GetById<AvailabilitySlot>(
            request.AvailabilityId,
            nameof(AvailabilitySlot.AvailabilityRule),
            "AvailabilityRule.Doctor.Speciality")
            ?? throw new EntityNotFoundException("AvailabilitySlot");

        var doctor = slot.AvailabilityRule?.Doctor
            ?? throw new EntityNotFoundException("Doctor");
        if (slot.AvailabilityRule.DoctorId != request.DoctorId)
            throw new ValidationException("El turno no pertenece al médico indicado.", "AVAILABILITY_SLOT_DOCTOR_MISMATCH")
                .WithDetail("availabilitySlotId", "El turno no pertenece al médico indicado.");

        if (slot.SlotDate.Date < DateTime.Today ||
            (slot.SlotDate.Date == DateTime.Today && slot.StartTime <= DateTime.Now.TimeOfDay))
            throw new BusinessRuleException("No se pueden reservar turnos en fechas u horarios pasados.", "APPOINTMENT_PAST_DATE");

        if (slot.Status != SlotStatus.AVAILABLE)
        {
            _logger.LogWarning("Intento de reserva sobre slot no disponible {SlotId}", slot.Id);
            throw new ConflictException("APPOINTMENT_CONFLICT", "El turno ya no está disponible.");
        }

        slot.Book();
        try
        {
            await _persistence.Update(slot);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning("Conflicto de concurrencia al reservar el slot {SlotId}", slot.Id);
            throw new ConflictException("APPOINTMENT_CONFLICT", "El turno ya no está disponible.");
        }

        var appointment = new Appointment(slot.Id, patient.Id, request.Reason.Trim());
        await _persistence.Add(appointment);
        _logger.LogInformation("Turno {AppointmentId} reservado para paciente {Dni} con doctor {DoctorId}",
            appointment.Id, request.Patient.Dni, request.DoctorId);

        return ToResponse(appointment, slot, doctor, patient);
    }

    public async Task<IEnumerable<AppointmentModel.Response>> GetByPatient(long dni)
    {
        var patient = await _patientService.GetByDniAsync(dni)
            ?? throw new EntityNotFoundException("Patient");
        var appointments = await _persistence.GetFiltered<Appointment>(
            a => a.PatientId == patient.Id && a.Status == AppointmentStatus.BOOKED,
            SlotIncludes1, SlotIncludes2);

        return (appointments ?? [])
            .Select(a => ToResponse(
                a,
                a.AvailabilitySlot!,
                a.AvailabilitySlot!.AvailabilityRule!.Doctor!,
                patient));
    }

    public async Task Cancel(Guid id)
    {
        var appointment = await _persistence.GetById<Appointment>(
            id, nameof(Appointment.AvailabilitySlot))
            ?? throw new EntityNotFoundException("Appointment");

        if (appointment.Status != AppointmentStatus.BOOKED)
        {
            _logger.LogWarning("Intento de cancelar turno {AppointmentId} en estado {Status}", appointment.Id, appointment.Status);
            throw new BusinessRuleException(
                "Solo se pueden cancelar turnos en estado reservado.",
                "APPOINTMENT_NOT_CANCELLABLE");
        }

        appointment.Cancel();
        await _persistence.Update(appointment);

        var slot = appointment.AvailabilitySlot
            ?? await _persistence.GetById<AvailabilitySlot>(appointment.AvailabilitySlotId);
        if (slot is not null)
        {
            slot.MakeAvailable();
            await _persistence.Update(slot);
        }

        _logger.LogInformation("Turno {AppointmentId} cancelado", appointment.Id);
    }

    public async Task<Pagination<AppointmentModel.SearchItem>> GetByDate(DateOnly date, int pageSize, int pageIndex)
    {
        var dateValue = date.ToDateTime(TimeOnly.MinValue).Date;
        var result = await _persistence.Paginate<Appointment, DateTime>(
            NormalizePageSize(pageSize),
            NormalizePageIndex(pageIndex),
            a => a.AvailabilitySlot!.SlotDate.Date == dateValue,
            a => a.AvailabilitySlot!.SlotDate,
            SlotIncludes1, SlotIncludes2);

        return result.Map(ToSearchItem);
    }

    public async Task<Pagination<AppointmentModel.SearchItem>> Search(Guid? specialtyId, Guid? doctorId, long? dni,
        DateOnly? date, int pageSize, int pageIndex)
    {
        if (dni.HasValue && (dni.Value < 1_000_000 || dni.Value > 99_999_999))
            throw new ValidationException()
                .WithDetail("dni", "El DNI debe tener 7 u 8 dígitos.");

        var dateValue = date?.ToDateTime(TimeOnly.MinValue).Date;
        var result = await _persistence.Paginate<Appointment, DateTime>(
            NormalizePageSize(pageSize),
            NormalizePageIndex(pageIndex),
            a => (!specialtyId.HasValue || a.AvailabilitySlot!.AvailabilityRule!.Doctor!.SpecialityId == specialtyId) &&
                 (!doctorId.HasValue || a.AvailabilitySlot!.AvailabilityRule!.DoctorId == doctorId) &&
                 (!dni.HasValue || a.Patient!.Dni == dni) &&
                 (!dateValue.HasValue || a.AvailabilitySlot!.SlotDate.Date == dateValue.Value),
            a => a.AvailabilitySlot!.SlotDate,
            SlotIncludes1, SlotIncludes2);

        return result.Map(ToSearchItem);
    }

    private static AppointmentModel.Response ToResponse(
        Appointment appointment,
        AvailabilitySlot slot,
        Doctor doctor,
        Patient patient) =>
        new(
            appointment.Id,
            doctor.Id,
            doctor.Name,
            doctor.Speciality?.Name ?? string.Empty,
            patient.Dni,
            patient.FullName ?? string.Empty,
            DateOnly.FromDateTime(slot.SlotDate),
            slot.StartTime.ToString(@"hh\:mm"),
            slot.EndTime.ToString(@"hh\:mm"),
            appointment.Reason,
            appointment.Status.ToString());

    private static AppointmentModel.SearchItem ToSearchItem(Appointment appointment)
    {
        var doctor = appointment.AvailabilitySlot!.AvailabilityRule!.Doctor!;
        var patient = appointment.Patient!;
        return new AppointmentModel.SearchItem(
            appointment.Id,
            appointment.Status.ToString(),
            new AppointmentModel.PatientSummary(patient.Dni, patient.FullName ?? string.Empty),
            new AppointmentModel.DoctorSummary(
                doctor.Id,
                doctor.Name,
                new AppointmentModel.SpecialtySummary(
                    doctor.Speciality?.Id ?? Guid.Empty,
                    doctor.Speciality?.Name ?? string.Empty)));
    }

    private static int NormalizePageSize(int pageSize) => pageSize > 0 ? pageSize : 10;

    private static int NormalizePageIndex(int pageIndex) => pageIndex > 0 ? pageIndex : 1;
}