using Dsw2026Tpi.CrossCutting.Exceptions;

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

    // Nombres de día del dominio (enunciado: LUNES..DOMINGO), tal como se persisten en DayOfWeek.
    private static readonly Dictionary<System.DayOfWeek, string> DayNames = new()
    {
        [System.DayOfWeek.Monday] = "LUNES",
        [System.DayOfWeek.Tuesday] = "MARTES",
        [System.DayOfWeek.Wednesday] = "MIÉRCOLES",
        [System.DayOfWeek.Thursday] = "JUEVES",
        [System.DayOfWeek.Friday] = "VIERNES",
        [System.DayOfWeek.Saturday] = "SÁBADO",
        [System.DayOfWeek.Sunday] = "DOMINGO",
    };

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

    public static bool IsValidDay(string day) => DayNames.ContainsValue(day);

    /// <summary>Indica si la regla corresponde a la fecha (mismo mes/año y mismo día de la semana).</summary>
    public bool AppliesTo(DateTime date) =>
        date.Year == Year && date.Month == Month && DayNames[date.DayOfWeek] == DayOfWeek;

    public bool HasSameSchedule(string day, TimeSpan start, TimeSpan end) =>
        DayOfWeek == day && StartTime == start && EndTime == end;

    public bool Overlaps(string day, TimeSpan start, TimeSpan end) =>
        DayOfWeek == day && start < EndTime && StartTime < end;

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

    /// <summary>
    /// Genera y agrega a la regla sus turnos de 30 minutos para el resto del mes (CU02),
    /// sin feriados/no laborales, sin horarios ya iniciados (RN04) y sin superponerse
    /// con turnos que se conservan (RN03).
    /// </summary>
    public IReadOnlyList<AvailabilitySlot> ScheduleSlots(DateTime now, IReadOnlySet<DateTime> nonWorkingDays,
        IReadOnlyCollection<AvailabilitySlot> occupiedSlots)
    {
        var scheduled = new List<AvailabilitySlot>();
        var lastDayOfMonth = new DateTime(Year, Month, DateTime.DaysInMonth(Year, Month));

        for (var date = now.Date; date <= lastDayOfMonth; date = date.AddDays(1))
        {
            if (!AppliesTo(date) || nonWorkingDays.Contains(date)) continue;

            foreach (var slot in GenerateSlots(date))
            {
                if (slot.HasStarted(now) || occupiedSlots.Any(o => o.Overlaps(slot))) continue;

                _availabilitySlots.Add(slot);
                scheduled.Add(slot);
            }
        }

        return scheduled;
    }

    /// <summary>
    /// Libera los turnos futuros que no están reservados, para que la disponibilidad pueda sobrescribirse:
    /// los libres se quitan de la regla y se devuelven para eliminarlos; los disponibles que ya tuvieron
    /// una cita (cancelada) se bloquean para no perder ese historial; los reservados y pasados no cambian.
    /// Requiere haber cargado <see cref="AvailabilitySlots"/> y sus <see cref="AvailabilitySlot.Appointments"/>.
    /// </summary>
    public IReadOnlyList<AvailabilitySlot> ReleaseUnbookedSlotsFrom(DateTime now)
    {
        var freeFutureSlots = _availabilitySlots.Where(s => s.IsFree && !s.HasStarted(now)).ToList();

        var released = freeFutureSlots.Where(s => !s.HasAppointmentHistory).ToList();
        foreach (var slot in released)
            _availabilitySlots.Remove(slot);

        foreach (var slot in freeFutureSlots.Where(s => s.HasAppointmentHistory))
            slot.Block();

        return released;
    }

    /// <summary>
    /// Baja lógica: la regla deja de ser la disponibilidad vigente del médico, pero se conserva
    /// porque sus turnos reservados o históricos la referencian.
    /// </summary>
    public void Retire() => Deleted = true;
}
