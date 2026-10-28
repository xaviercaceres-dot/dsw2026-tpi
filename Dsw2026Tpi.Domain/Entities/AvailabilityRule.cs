using Dsw2026Tpi.CrossCutting.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Domain.Entities;

public class AvailabilityRule : EntityBase
{
    public Guid DoctorId { get; init; }
    public Doctor? Doctor { get; init; }
    public int Month { get; init; }
    public int Year { get; init; }
    public string? DayOfWeek { get; init; }
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }

    private static readonly TimeSpan SlotDuration = TimeSpan.FromMinutes(30);

    private readonly List<AvailabilitySlot> _availabilitySlots = new List<AvailabilitySlot>();
    public IReadOnlyCollection<AvailabilitySlot> AvailabilitySlots => _availabilitySlots.AsReadOnly();
    public bool Deleted { get; private set; } = false;

    #region Constructor for EF
#pragma warning disable CS8618
    private AvailabilityRule() { }
#pragma warning restore CS8618
    #endregion


    public AvailabilityRule(Guid doctorId, int month, int year, string dayOfWeek, TimeSpan startTime, TimeSpan endTime, Guid? id = null) : base(id)
    {
        DoctorId = doctorId;
        Month = month;
        Year = year;
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
    }

    public List<AvailabilitySlot> GenerateSlots(DateTime targetDate)
    {
        if (targetDate.Month != Month || targetDate.Year != Year)
            throw new BusinessRuleException("La fecha no coincide con el mes/año de esta regla de disponibilidad.", "SLOT_GENERATION_INVALID_DATE");

        var slots = new List<AvailabilitySlot>();
        var currentTime = StartTime;

        while (currentTime.Add(SlotDuration) <= EndTime)
        {
            var slot = new AvailabilitySlot(
                this.Id,
                targetDate.Date,
                currentTime,
                currentTime.Add(SlotDuration)
            );

            slots.Add(slot);
            currentTime = currentTime.Add(SlotDuration);
        }

        return slots;
    }
}
