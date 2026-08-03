using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;



namespace Dsw2026Tpi.Application.Services;

public class AppointmentService : IAppointmentService
{
   
    private const string SlotIncludes1 = "AvailabilitySlot.AvailabilityRule.Doctor.Speciality";
    private const string SlotIncludes2 = "Patient";

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
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length < 5)
            throw new ValidationException().WithDetail("reason", "El motivo es obligatorio (mínimo 5 caracteres).");

        _ = await _persistence.GetById<Doctor>(request.DoctorId) ?? throw new EntityNotFoundException("Doctor");

        var patient = await _patientService.GetByDniAsync(request.Patient.Dni)
            ?? throw new EntityNotFoundException("Patient");

        var slot = await _persistence.GetById<AvailabilitySlot>(request.AvailabilityId, "AvailabilityRule", "Doctor.Speciality")
      ?? throw new EntityNotFoundException("AvailabilitySlot");


        if (slot.AvailabilityRule?.DoctorId != request.DoctorId)
            throw new ValidationException().WithDetail("availabilityId", "El turno no pertenece al médico indicado.");

        if (slot.SlotDate.Date < DateTime.Today || (slot.SlotDate.Date == DateTime.Today && slot.StartTime < DateTime.Now.TimeOfDay))
            throw new BusinessRuleException("No se pueden reservar turnos en fechas u horarios pasados.", "APPOINTMENT_PAST_DATE");

        if (slot.Status != "AVAILABLE")
        {
            _logger.LogWarning("Intento de reserva sobre slot no disponible {SlotId} (doctor {DoctorId})", slot.Id, slot.DoctorId);
            throw new ConflictException("APPOINTMENT_CONFLICT", "Slot already booked");
        }

        slot.Book();
        try
        {
            await _persistence.Update(slot);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning("Conflicto de concurrencia al reservar el slot {SlotId}", slot.Id);
            throw new ConflictException("APPOINTMENT_CONFLICT", "Slot already booked");
        }

        var appointment = new Appointment(slot.Id, patient.Id, request.Reason);
        await _persistence.Add(appointment);
        _logger.LogInformation("Turno {AppointmentId} reservado para paciente {Dni} con doctor {DoctorId}",
            appointment.Id, request.Patient.Dni, request.DoctorId);

        return ToResponse(appointment, slot, slot.Doctor!, patient);
    }

    public async Task<IEnumerable<AppointmentModel.Response>> GetByPatient(long dni)
    {
        var patient = await _patientService.GetByDniAsync(dni) ?? throw new EntityNotFoundException("Patient");

        var appointments = await _persistence.GetFiltered<Appointment>(
            a => a.PatientId == patient.Id && a.Status == "BOOKED", SlotIncludes1, SlotIncludes2);

        return (appointments ?? []).Select(a => ToResponse(a, a.AvailabilitySlot!, a.AvailabilitySlot!.AvailabilityRule!.Doctor!, patient));
    }

    public async Task Cancel(Guid id)
    {
        var appointment = await _persistence.GetById<Appointment>(id, nameof(Appointment.AvailabilitySlot))
            ?? throw new EntityNotFoundException("Appointment");

        if (appointment.Status != "BOOKED")
        {
            _logger.LogWarning("Intento de cancelar turno {AppointmentId} en estado {Status}", appointment.Id, appointment.Status);
            throw new BusinessRuleException("Solo se pueden cancelar turnos en estado reservado.", "APPOINTMENT_NOT_CANCELLABLE");
        }

        appointment.Cancel();
        await _persistence.Update(appointment);

        var slot = appointment.AvailabilitySlot ?? await _persistence.GetById<AvailabilitySlot>(appointment.AvailabilitySlotId);
        if (slot is not null)
        {
            slot.MakeAvailable();
            await _persistence.Update(slot);
        }

        _logger.LogInformation("Turno {AppointmentId} cancelado", appointment.Id);
    }

    public async Task<IEnumerable<AppointmentModel.Response>> GetByDate(DateOnly date)
    {
        var appointments = await _persistence.GetFiltered<Appointment>(
            a => a.AvailabilitySlot!.SlotDate.Date == date.ToDateTime(TimeOnly.MinValue).Date, SlotIncludes1, SlotIncludes2);

        return (appointments ?? []).Select(a => ToResponse(a, a.AvailabilitySlot!, a.AvailabilitySlot!.AvailabilityRule!.Doctor!, a.Patient!));
    }

    public async Task<Pagination<AppointmentModel.Response>> Search(Guid? specialtyId, Guid? doctorId, long? dni,
        DateOnly? date, int pageSize, int pageIndex)
    {
        var dateValue = date?.ToDateTime(TimeOnly.MinValue).Date;

        var result = await _persistence.Paginate<Appointment, DateTime>(pageSize, pageIndex,
            a => (!specialtyId.HasValue || a.AvailabilitySlot!.AvailabilityRule!.Doctor!.SpecialityId == specialtyId) &&
                 (!doctorId.HasValue || a.AvailabilitySlot!.AvailabilityRule!.DoctorId == doctorId) && 
                 (!dni.HasValue || a.Patient!.Dni == dni) &&
                 (!dateValue.HasValue || a.AvailabilitySlot!.SlotDate.Date == dateValue.Value),
            a => a.AvailabilitySlot!.SlotDate,
            SlotIncludes1, SlotIncludes2);

       
        return result.Map(a => ToResponse(a, a.AvailabilitySlot!, a.AvailabilitySlot!.AvailabilityRule!.Doctor!, a.Patient!));
    }

    private static AppointmentModel.Response ToResponse(Appointment a, AvailabilitySlot slot, Doctor doctor, Patient patient) =>
        new(a.Id, doctor.Id, doctor.Name, doctor.Speciality?.Name ?? string.Empty,
            patient.Dni, patient.Name, DateOnly.FromDateTime(slot.SlotDate),
            slot.StartTime.ToString(@"hh\:mm"), slot.EndTime.ToString(@"hh\:mm"), a.Reason, a.Status);
}