using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Enums;

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

    /// <summary>
    /// Cancela la cita y libera su turno (CU03). Solo se permite en estado BOOKED.
    /// Requiere haber cargado la navegación <see cref="AvailabilitySlot"/>.
    /// </summary>
    public void Cancel()
    {
        if (Status != AppointmentStatus.BOOKED)
            throw new BusinessRuleException("Solo se pueden cancelar citas en estado reservado.", "APPOINTMENT_CANNOT_BE_CANCELLED");

        if (AvailabilitySlot is null)
            throw new InvalidOperationException("Debe cargarse el turno de la cita para poder cancelarla.");

        Status = AppointmentStatus.CANCELLED;
        CancelledAt = DateTime.UtcNow;
        AvailabilitySlot.Release();
    }

    public void Attend()
    {
        if (Status != AppointmentStatus.BOOKED)
            throw new BusinessRuleException("Solo se pueden atender citas previamente reservadas.", "APPOINTMENT_CANNOT_BE_ATTENDED");

        Status = AppointmentStatus.ATTENDED;
        AttendedAt = DateTime.UtcNow;
    }
}
