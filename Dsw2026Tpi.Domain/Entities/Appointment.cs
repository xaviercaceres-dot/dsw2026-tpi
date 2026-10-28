using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Domain.Entities;

public class Appointment : EntityBase
{
    public Guid AvailabilitySlotId { get; init; }
    public AvailabilitySlot? AvailabilitySlot { get; init; }
    public Guid PatientId { get; init; }
    public Patient? Patient { get; init; }
    public string Reason { get; private set; }
    public AppointmentStatus Status { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public DateTime? AttendedAt { get; private set; }

#pragma warning disable CS8618
    private Appointment() { }
#pragma warning restore CS8618

    public Appointment(Guid availabilitySlotId, Guid patientId, string reason, Guid? id = null) : base(id)
    {
        AvailabilitySlotId = availabilitySlotId;
        PatientId = patientId;
        Reason = reason;
        Status = AppointmentStatus.BOOKED;
    }

    public void Cancel()
    {
        if (Status == AppointmentStatus.CANCELLED || Status == AppointmentStatus.ATTENDED)
            throw new BusinessRuleException("No se puede cancelar una cita que ya fue atendida o cancelada.","APPOINTMENT_CANNOT_BE_CANCELLED");

        Status = AppointmentStatus.CANCELLED;
        CancelledAt = DateTime.UtcNow;
    }

    public void Attend()
    {
        if (Status != AppointmentStatus.BOOKED)
            throw new BusinessRuleException("Solo se pueden atender citas previamente reservadas.", "APPOINTMENT_CANNOT_BE_ATTENDED");

        Status = AppointmentStatus.ATTENDED;
        AttendedAt = DateTime.UtcNow;
    }

}
