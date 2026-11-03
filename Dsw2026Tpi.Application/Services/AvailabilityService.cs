using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Enums;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dsw2026Tpi.Application.Services;

public class AvailabilityService : IAvailabilityService
{
    private static readonly string[] ValidDays =
        ["LUNES", "MARTES", "MIÉRCOLES", "JUEVES", "VIERNES", "SÁBADO", "DOMINGO"];

    private static readonly Dictionary<DayOfWeek, string> DayMap = new()
    {
        [DayOfWeek.Monday] = "LUNES",
        [DayOfWeek.Tuesday] = "MARTES",
        [DayOfWeek.Wednesday] = "MIÉRCOLES",
        [DayOfWeek.Thursday] = "JUEVES",
        [DayOfWeek.Friday] = "VIERNES",
        [DayOfWeek.Saturday] = "SÁBADO",
        [DayOfWeek.Sunday] = "DOMINGO",
    };

    private static readonly TimeSpan SlotDuration = TimeSpan.FromMinutes(30);

    private readonly IPersistence _persistence;
    private readonly ILogger<AvailabilityService> _logger;
    private readonly IHolidayProvider _holidayProvider;

    public AvailabilityService(IPersistence persistence, ILogger<AvailabilityService> logger, IHolidayProvider holidayProvider)
    {
        _persistence = persistence;
        _logger = logger;
        _holidayProvider = holidayProvider;
    }

    public async Task<IEnumerable<AvailabilityModel.RuleDto>> Create(AvailabilityModel.Request request) => await SetAvailability(request, overwrite: false);
    public async Task<IEnumerable<AvailabilityModel.RuleDto>> Update(AvailabilityModel.Request request) => await SetAvailability(request, overwrite: true);

    public async Task<IEnumerable<AvailabilityModel.SlotDto>> GetAvailableSlots(Guid doctorId, DateOnly date)
    {
        _ = await _persistence.GetById<Doctor>(doctorId) ?? throw new EntityNotFoundException("Doctor");

        var dateValue = date.ToDateTime(TimeOnly.MinValue).Date;
        var rules = await _persistence.GetFiltered<AvailabilityRule>(
            r => r.DoctorId == doctorId && r.Month == dateValue.Month && r.Year == dateValue.Year,
            nameof(AvailabilityRule.AvailabilitySlots));

        var slots = rules?
            .SelectMany(r => r.AvailabilitySlots)
            .Where(s => s.SlotDate.Date == dateValue && s.Status == SlotStatus.AVAILABLE)
            .OrderBy(s => s.StartTime)
            .Select(s => new AvailabilityModel.SlotDto(
                s.Id,
                Format(s.StartTime),
                Format(s.EndTime),
                s.Status.ToString()));

        return slots ?? [];
    }

    private async Task<IEnumerable<AvailabilityModel.RuleDto>> SetAvailability(AvailabilityModel.Request request, bool overwrite)
    {
        _ = await _persistence.GetById<Doctor>(request.DoctorId) ?? throw new EntityNotFoundException("Doctor");
        var parsedDays = ParseAndValidate(request.Days);
        var today = DateTime.Today;
        var month = today.Month;
        var year = today.Year;
        var holidays = await _holidayProvider.GetHolidaysAsync();

        var existingRules = (await _persistence.GetFiltered<AvailabilityRule>(
            r => r.DoctorId == request.DoctorId && r.Month == month && r.Year == year))?.ToList() ?? [];

        if (overwrite)
        {
            var protectedRuleIds = (await _persistence.GetFiltered<AvailabilitySlot>(
                s => s.AvailabilityRule!.DoctorId == request.DoctorId &&
                     s.SlotDate.Month == month &&
                     s.SlotDate.Year == year &&
                     (s.Status == SlotStatus.BOOKED || s.Appointment != null),
                nameof(AvailabilitySlot.Appointment)))?
                .Select(s => s.AvailabilityRuleId)
                .ToHashSet() ?? [];

            var replaceableRules = existingRules
                .Where(r => !protectedRuleIds.Contains(r.Id))
                .ToList();
            if (replaceableRules.Count > 0)
                await _persistence.DeleteRange(replaceableRules);

            existingRules = existingRules.Where(r => protectedRuleIds.Contains(r.Id)).ToList();
        }

        var affectedRules = new List<AvailabilityRule>();
        var slotsToInsert = new List<AvailabilitySlot>();

        foreach (var (day, start, end) in parsedDays)
        {
            var sameDay = existingRules.Where(r => r.DayOfWeek == day).ToList();
            if (!overwrite)
            {
                if (sameDay.Any(r => r.StartTime == start && r.EndTime == end))
                    continue;
                if (sameDay.Any(r => start < r.EndTime && r.StartTime < end))
                    throw new ConflictException("AVAILABILITY_OVERLAP", $"Ya existe disponibilidad para {day} que se superpone.");
            }

            var rule = new AvailabilityRule(request.DoctorId, month, year, day, start, end);
            await _persistence.Add(rule);
            affectedRules.Add(rule);

            for (var currentDate = today; currentDate.Year == year && currentDate.Month == month; currentDate = currentDate.AddDays(1))
            {
                if (DayMap[currentDate.DayOfWeek] != day || holidays.Contains(currentDate.Date))
                    continue;

                for (var slotStart = start; slotStart + SlotDuration <= end; slotStart += SlotDuration)
                    slotsToInsert.Add(new AvailabilitySlot(rule.Id, currentDate.Date, slotStart, slotStart + SlotDuration));
            }
        }

        if (slotsToInsert.Count > 0)
            await _persistence.AddRange(slotsToInsert);

        _logger.LogInformation("Disponibilidad {Mode} para doctor {DoctorId}: {Count} regla(s)",
            overwrite ? "sobrescrita" : "creada", request.DoctorId, affectedRules.Count);

        return affectedRules.Select(r => new AvailabilityModel.RuleDto(
            r.Id, r.DayOfWeek!, Format(r.StartTime), Format(r.EndTime)));
    }

    private static List<(string Day, TimeSpan Start, TimeSpan End)> ParseAndValidate(List<AvailabilityModel.DayScheduleDto>? days)
    {
        if (days is null || days.Count == 0)
            throw new ValidationException("Debe indicar al menos un día de disponibilidad.", "AVAILABILITY_DAYS_REQUIRED");

        var parsed = new List<(string Day, TimeSpan Start, TimeSpan End)>();

        foreach (var d in days)
        {
            var day = d.Day?.Trim().ToUpperInvariant() ?? string.Empty;

            if (!ValidDays.Contains(day))
                throw new ValidationException($"El día '{d.Day}' no es válido.", "INVALID_DAY")
                    .WithDetail("day", d.Day ?? string.Empty);

            if (!TimeSpan.TryParse(d.StartTime, out var start) || !TimeSpan.TryParse(d.EndTime, out var end))
                throw new ValidationException("El formato de horario debe ser HH:mm.", "INVALID_TIME_FORMAT");

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

    private static string Format(TimeSpan time) => time.ToString(@"hh\:mm");
}