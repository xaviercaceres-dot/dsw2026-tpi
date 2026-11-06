using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Enums;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Dsw2026Tpi.Application.Services;

/// <summary>
/// Controlador del caso de uso CU02 - Gestionar Disponibilidad y de la consulta de turnos libres (CU01, paso 4).
/// Valida el contrato de entrada, obtiene los objetos del dominio, les delega las reglas y persiste.
/// </summary>
public class AvailabilityService : IAvailabilityService
{
    private static readonly string[] TimeFormats = ["HH:mm", "H:mm"];

    private static readonly string[] RuleWithSlotsIncludes =
    [
        nameof(AvailabilityRule.AvailabilitySlots),
        $"{nameof(AvailabilityRule.AvailabilitySlots)}.{nameof(AvailabilitySlot.Appointments)}"
    ];

    private readonly IPersistence _persistence;
    private readonly IHolidayProvider _holidayProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AvailabilityService> _logger;

    public AvailabilityService(IPersistence persistence, IHolidayProvider holidayProvider,
        TimeProvider timeProvider, ILogger<AvailabilityService> logger)
    {
        _persistence = persistence;
        _holidayProvider = holidayProvider;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// POST: agrega franjas al mes en curso. Una franja idéntica a una vigente se ignora;
    /// una que se superpone con una vigente es un conflicto.
    /// </summary>
    public async Task<IEnumerable<AvailabilityModel.RuleDto>> Create(AvailabilityModel.Request request)
    {
        var days = ParseAndValidate(request);
        await EnsureActiveDoctor(request.DoctorId);
        var now = Now();

        var currentRules = await GetCurrentRules(request.DoctorId, now);
        var newDays = days
            .Where(d => !currentRules.Any(r => r.HasSameSchedule(d.Day, d.Start, d.End)))
            .ToList();

        var overlapped = newDays.FirstOrDefault(d => currentRules.Any(r => r.Overlaps(d.Day, d.Start, d.End)));
        if (overlapped != default)
            throw new ConflictException("AVAILABILITY_OVERLAP", $"Ya existe disponibilidad para {overlapped.Day} que se superpone.");

        var rules = await CreateRulesForRestOfMonth(request.DoctorId, newDays, now);

        _logger.LogInformation("Disponibilidad creada para doctor {DoctorId}: {Count} regla(s)", request.DoctorId, rules.Count);
        return rules.Select(ToRuleDto);
    }

    /// <summary>
    /// PUT: reemplaza la disponibilidad del mes en curso sobrescribiendo los turnos futuros NO reservados.
    /// Las franjas idénticas a las vigentes se conservan; las demás se dan de baja liberando sus turnos libres.
    /// Los turnos reservados se respetan y los nuevos turnos no se superponen con ellos.
    /// Todos los cambios se persisten en un único guardado (una transacción).
    /// </summary>
    public async Task<IEnumerable<AvailabilityModel.RuleDto>> Update(AvailabilityModel.Request request)
    {
        var days = ParseAndValidate(request);
        await EnsureActiveDoctor(request.DoctorId);
        var now = Now();

        var currentRules = await GetCurrentRules(request.DoctorId, now);
        var keptRules = currentRules
            .Where(r => days.Any(d => r.HasSameSchedule(d.Day, d.Start, d.End)))
            .ToList();
        var (retiredRules, releasedSlotIds) = await RetireRules(currentRules.Except(keptRules), now);

        var newDays = days.Where(d => !keptRules.Any(r => r.HasSameSchedule(d.Day, d.Start, d.End)));
        var newRules = await CreateRulesForRestOfMonth(request.DoctorId, newDays, now, releasedSlotIds);

        // Sin reglas nuevas todavía no hubo guardado: un único guardado confirma todas las bajas, porque las reglas
        // se cargaron con seguimiento de cambios (incluye cada regla dada de baja y sus turnos liberados o bloqueados).
        if (newRules.Count == 0 && retiredRules.Count > 0)
            await _persistence.Update(retiredRules[0]);

        _logger.LogInformation("Disponibilidad sobrescrita para doctor {DoctorId}: {Kept} regla(s) conservadas, {New} nuevas, {Retired} dadas de baja y {Released} turno(s) libre(s) eliminados",
            request.DoctorId, keptRules.Count, newRules.Count, retiredRules.Count, releasedSlotIds.Count);

        var schedule = keptRules.Concat(newRules).ToList();
        return days.Select(d => ToRuleDto(schedule.First(r => r.HasSameSchedule(d.Day, d.Start, d.End))));
    }

    public async Task<IEnumerable<AvailabilityModel.SlotDto>> GetAvailableSlots(Guid doctorId, DateOnly date)
    {
        var ex = new ValidationException();
        if (doctorId == Guid.Empty)
            ex.WithDetail("doctorId", "El médico es obligatorio.");
        if (date == default)
            ex.WithDetail("date", "La fecha es obligatoria (formato YYYY-MM-DD).");
        if (ex.Error.Details.Count > 0) throw ex;

        await EnsureActiveDoctor(doctorId);
        var now = Now();
        var day = date.ToDateTime(TimeOnly.MinValue);

        if (day < now.Date) return [];

        var slots = await _persistence.GetFiltered<AvailabilitySlot>(s =>
            s.AvailabilityRule!.DoctorId == doctorId &&
            s.SlotDate == day &&
            s.Status == SlotStatus.AVAILABLE) ?? [];

        return slots
            .Where(s => !s.HasStarted(now))
            .OrderBy(s => s.StartTime)
            .Select(s => new AvailabilityModel.SlotDto(s.Id, Format(s.StartTime), Format(s.EndTime), s.Status.ToString()))
            .ToList();
    }

    /// <summary>
    /// Da de baja las reglas indicadas y libera sus turnos futuros libres, sin guardar todavía: las reglas se cargan
    /// con GetById (con seguimiento de cambios) y los turnos liberados se quitan de su regla, por lo que se eliminan
    /// en el próximo guardado junto con el resto de los cambios (conservando el control de concurrencia por RowVersion).
    /// </summary>
    private async Task<(List<AvailabilityRule> Retired, HashSet<Guid> ReleasedSlotIds)> RetireRules(
        IEnumerable<AvailabilityRule> rules, DateTime now)
    {
        var retired = new List<AvailabilityRule>();
        var releasedSlotIds = new HashSet<Guid>();

        foreach (var current in rules)
        {
            var rule = await _persistence.GetById<AvailabilityRule>(current.Id, RuleWithSlotsIncludes);
            if (rule is null) continue;

            foreach (var slot in rule.ReleaseUnbookedSlotsFrom(now))
                releasedSlotIds.Add(slot.Id);
            rule.Retire();
            retired.Add(rule);
        }

        return (retired, releasedSlotIds);
    }

    /// <summary>
    /// Crea las reglas del mes en curso; cada regla genera sus turnos y se persiste junto con ellos.
    /// Los turnos que se están liberando (<paramref name="releasedSlotIds"/>) no cuentan como ocupados.
    /// </summary>
    private async Task<List<AvailabilityRule>> CreateRulesForRestOfMonth(Guid doctorId,
        IEnumerable<(string Day, TimeSpan Start, TimeSpan End)> days, DateTime now, IReadOnlySet<Guid>? releasedSlotIds = null)
    {
        var rules = days
            .Select(d => new AvailabilityRule(doctorId, now.Month, now.Year, d.Day, d.Start, d.End))
            .ToList();

        if (rules.Count == 0) return rules;

        var holidays = await _holidayProvider.GetHolidaysAsync();
        var occupiedSlots = (await GetUpcomingSlots(doctorId, now))
            .Where(s => releasedSlotIds is null || !releasedSlotIds.Contains(s.Id))
            .ToList();
        var slotCount = rules.Sum(r => r.ScheduleSlots(now, holidays, occupiedSlots).Count);

        await _persistence.AddRange(rules); // único guardado: altas y, en un PUT, también las bajas pendientes

        _logger.LogInformation("Doctor {DoctorId}: {Slots} turno(s) generados para {Month}/{Year}",
            doctorId, slotCount, now.Month, now.Year);
        return rules;
    }

    private async Task<List<AvailabilityRule>> GetCurrentRules(Guid doctorId, DateTime now) =>
        (await _persistence.GetFiltered<AvailabilityRule>(r =>
            r.DoctorId == doctorId && r.Month == now.Month && r.Year == now.Year && !r.Deleted) ?? []).ToList();

    private async Task<List<AvailabilitySlot>> GetUpcomingSlots(Guid doctorId, DateTime now)
    {
        var today = now.Date;
        var lastDayOfMonth = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month));

        return (await _persistence.GetFiltered<AvailabilitySlot>(s =>
            s.AvailabilityRule!.DoctorId == doctorId &&
            s.SlotDate >= today && s.SlotDate <= lastDayOfMonth) ?? []).ToList();
    }

    private async Task EnsureActiveDoctor(Guid doctorId)
    {
        var doctor = await _persistence.GetById<Doctor>(doctorId);
        if (doctor is null || !doctor.IsActive)
            throw new EntityNotFoundException(nameof(Doctor));
    }

    private DateTime Now() => _timeProvider.GetLocalNow().DateTime;

    private static List<(string Day, TimeSpan Start, TimeSpan End)> ParseAndValidate(AvailabilityModel.Request? request)
    {
        if (request is null)
            throw new ValidationException("El cuerpo de la solicitud es obligatorio.", "AVAILABILITY_REQUEST_REQUIRED");

        if (request.DoctorId == Guid.Empty)
            throw new ValidationException().WithDetail("doctorId", "El médico es obligatorio.");

        if (request.Days is null || request.Days.Count == 0)
            throw new ValidationException("Debe indicar al menos un día de disponibilidad.", "AVAILABILITY_DAYS_REQUIRED");

        var parsed = new List<(string Day, TimeSpan Start, TimeSpan End)>();

        foreach (var d in request.Days)
        {
            var day = d?.Day?.Trim().ToUpperInvariant() ?? string.Empty;

            if (!AvailabilityRule.IsValidDay(day))
                throw new ValidationException($"El día '{d?.Day}' no es válido.", "INVALID_DAY")
                    .WithDetail("day", d?.Day ?? string.Empty);

            if (!TryParseTime(d!.StartTime, out var start) || !TryParseTime(d.EndTime, out var end))
                throw new ValidationException("El formato de horario debe ser HH:mm.", "INVALID_TIME_FORMAT")
                    .WithDetail("day", day);

            if (start >= end)
                throw new BusinessRuleException("El horario de inicio debe ser anterior al horario de fin.", "INVALID_TIME_RANGE")
                    .WithDetail("day", day);

            parsed.Add((day, start, end));
        }

        foreach (var group in parsed.GroupBy(d => d.Day))
        {
            var ordered = group.OrderBy(d => d.Start).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].Start < ordered[i - 1].End)
                    throw new ConflictException("AVAILABILITY_OVERLAP", $"Los horarios indicados para el día {group.Key} se superponen.");
            }
        }

        return parsed;
    }

    private static bool TryParseTime(string? value, out TimeSpan time)
    {
        time = default;
        if (!TimeOnly.TryParseExact(value?.Trim(), TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return false;

        time = parsed.ToTimeSpan();
        return true;
    }

    private static AvailabilityModel.RuleDto ToRuleDto(AvailabilityRule r) =>
        new(r.Id, r.DayOfWeek!, Format(r.StartTime), Format(r.EndTime));

    private static string Format(TimeSpan time) => time.ToString(@"hh\:mm");
}
