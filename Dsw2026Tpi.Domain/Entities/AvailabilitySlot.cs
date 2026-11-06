using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Enums;

namespace Dsw2026Tpi.Domain.Entities;

public class AvailabilitySlot : EntityBase
{
    public Guid AvailabilityRuleId { get; init; }
    public AvailabilityRule? AvailabilityRule { get; private set; }
    public DateTime SlotDate { get; init; }
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }
    public SlotStatus Status { get; private set; }
    public bool Deleted { get; private set; } = false;

    // Historial de citas del turno. Como máximo una puede estar reservada (BOOKED) a la vez;
    // las canceladas se conservan como registro y el turno vuelve a estar disponible (CU03).
    private readonly List<Appointment> _appointments = new List<Appointment>();
    public IReadOnlyCollection<Appointment> Appointments => _appointments.AsReadOnly();

#pragma warning disable CS8618
    private AvailabilitySlot() { }
#pragma warning restore CS8618

    public AvailabilitySlot(Guid availabilityRuleId, DateTime slotDate, TimeSpan startTime, TimeSpan endTime, Guid? id = null) : base(id)
    {
        AvailabilityRuleId = availabilityRuleId;
        SlotDate = slotDate;
        StartTime = startTime;
        EndTime = endTime;
        Status = SlotStatus.AVAILABLE;
    }

    /// <summary>Un turno es reservable mientras esté disponible (RN03).</summary>
    public bool IsFree => Status == SlotStatus.AVAILABLE;

    /// <summary>
    /// Indica si el turno tuvo citas (por ejemplo, canceladas): en ese caso no puede eliminarse sin perder el historial.
    /// Requiere haber cargado la navegación <see cref="Appointments"/>.
    /// </summary>
    public bool HasAppointmentHistory => _appointments.Count > 0;

    public DateTime StartsAt => SlotDate.Date + StartTime;

    public bool HasStarted(DateTime now) => StartsAt <= now;

    public bool Overlaps(AvailabilitySlot other) =>
        SlotDate.Date == other.SlotDate.Date && StartTime < other.EndTime && other.StartTime < EndTime;

    /// <summary>
    /// Indica si el turno pertenece al médico, a través de su regla de disponibilidad.
    /// Requiere haber cargado la navegación <see cref="AvailabilityRule"/>.
    /// </summary>
    public bool BelongsTo(Guid doctorId)
    {
        if (AvailabilityRule is null)
            throw new InvalidOperationException("Debe cargarse la regla de disponibilidad del turno para conocer su médico.");

        return AvailabilityRule.DoctorId == doctorId;
    }

    /// <summary>
    /// Reserva el turno para un paciente y crea la cita correspondiente (RN03, RN04).
    /// </summary>
    public Appointment Book(Guid patientId, string reason, DateTime now)
    {
        if (HasStarted(now))
            throw new BusinessRuleException("No se pueden reservar turnos en fechas u horarios pasados.", "APPOINTMENT_PAST_DATE");

        if (!IsFree)
            throw new ConflictException("APPOINTMENT_CONFLICT", "Slot already booked")
                .WithDetail("availabilitySlotId", "slot_unavailable");

        Status = SlotStatus.BOOKED;
        var appointment = new Appointment(Id, patientId, reason) { AvailabilitySlot = this };
        _appointments.Add(appointment);
        return appointment;
    }

    /// <summary>
    /// Libera un turno reservado (al cancelar su cita) para que vuelva a estar disponible.
    /// </summary>
    public void Release()
    {
        if (Status != SlotStatus.BOOKED)
            throw new BusinessRuleException("Solo se pueden liberar turnos reservados.", "SLOT_CANNOT_BE_RELEASED");

        Status = SlotStatus.AVAILABLE;
    }

    public void Block()
    {
        if (Status != SlotStatus.AVAILABLE)
            throw new BusinessRuleException("Solo se pueden bloquear turnos disponibles.", "SLOT_CANNOT_BE_BLOCKED");
        Status = SlotStatus.BLOCKED;
    }
}
