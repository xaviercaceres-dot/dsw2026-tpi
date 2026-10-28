using Dsw2026Tpi.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Domain.Entities;

public class AvailabilitySlot : EntityBase

{
    public Guid AvailabilityRuleId { get; init; }
    public AvailabilityRule? AvailabilityRule { get; private set; }
    public DateTime SlotDate { get; init; }
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }
    public SlotStatus Status { get; private set; }
    public Appointment? Appointment { get; private set; }

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

    public void Block()
    {
        if (Status != SlotStatus.AVAILABLE)
            throw new InvalidOperationException("Solo se pueden bloquear turnos disponibles.");
        Status = SlotStatus.BLOCKED;
    }

    public void Book()
    {
        if (Status != SlotStatus.AVAILABLE)
            throw new InvalidOperationException("El turno no está disponible para reserva.");
        Status = SlotStatus.BOOKED;
    }

    public void MakeAvailable()
    {
        Status = SlotStatus.AVAILABLE;
    }

}
