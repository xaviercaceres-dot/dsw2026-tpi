using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Application.Services
{
    
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

            public AvailabilityService(IPersistence persistence, ILogger<AvailabilityService> logger)
            {
                _persistence = persistence;
                _logger = logger;
            }

            public Task Create(AvailabilityModel.Request request) => SetAvailability(request);

            public Task Update(AvailabilityModel.Request request) => SetAvailability(request);

            public async Task<IEnumerable<AvailabilityModel.SlotDto>> GetAvailableSlots(Guid doctorId, DateOnly date)
            {
                _ = await _persistence.GetById<Doctor>(doctorId) ?? throw new EntityNotFoundException("Doctor");

                var dateValue = date.ToDateTime(TimeOnly.MinValue).Date;
                var slots = await _persistence.GetFiltered<AvailabilitySlot>(
                    s => s.DoctorId == doctorId && s.SlotDate.Date == dateValue && s.Status == "AVAILABLE");

                return (slots ?? [])
                    .OrderBy(s => s.StartTime)
                    .Select(s => new AvailabilityModel.SlotDto(s.Id, Format(s.StartTime), Format(s.EndTime), s.Status));
            }

            private async Task SetAvailability(AvailabilityModel.Request request)
            {
                _ = await _persistence.GetById<Doctor>(request.DoctorId) ?? throw new EntityNotFoundException("Doctor");

                var parsedDays = ParseAndValidate(request.Days);

                var today = DateTime.Today;
                var month = today.Month;
                var year = today.Year;

                await ClearCurrentMonth(request.DoctorId, month, year);

                var lastDay = DateTime.DaysInMonth(year, month);

                foreach (var (day, start, end) in parsedDays)
                {
                    var rule = new AvailabilityRule(request.DoctorId, month, year, day, start, end);
                    await _persistence.Add(rule);

                    for (var date = today; date.Day <= lastDay; date = date.AddDays(1))
                    {
                        if (DayMap[date.DayOfWeek] != day) continue;

                        for (var slotStart = start; slotStart + SlotDuration <= end; slotStart += SlotDuration)
                        {
                            var slotEnd = slotStart + SlotDuration;
                            await _persistence.Add(new AvailabilitySlot(rule.Id, request.DoctorId, date.Date, slotStart, slotEnd));
                        }
                    }
                }

                _logger.LogInformation("Disponibilidad configurada para el médico {DoctorId}: {Days} días, período {Month}/{Year}",
                    request.DoctorId, parsedDays.Count, month, year);
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

            private async Task ClearCurrentMonth(Guid doctorId, int month, int year)
            {
                var existingSlots = await _persistence.GetFiltered<AvailabilitySlot>(
                    s => s.DoctorId == doctorId && s.SlotDate.Month == month && s.SlotDate.Year == year);

                if (existingSlots is not null)
                    foreach (var slot in existingSlots)
                        await _persistence.Delete(slot);

                var existingRules = await _persistence.GetFiltered<AvailabilityRule>(
                    r => r.DoctorId == doctorId && r.Month == month && r.Year == year);

                if (existingRules is not null)
                    foreach (var rule in existingRules)
                        await _persistence.Delete(rule);
            }

            private static string Format(TimeSpan time) => time.ToString(@"hh\:mm");
        }
    }
