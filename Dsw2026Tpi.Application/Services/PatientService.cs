using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Application.Services;

public class PatientService : IPatientService
{
    private readonly IPersistence _persistence;
    public PatientService(IPersistence persistence)
    {
        _persistence = persistence;
    }

    public async Task AddAsync(Patient patient)
    {
        await _persistence.Add(patient);
    }

    public async Task<Patient?> GetByDniAsync(string dni)
    {
        return await _persistence.First<Patient>(p => p.Dni == dni);
    }

    public async Task<Patient?> GetByIdAsync(Guid id)
    {
        return await _persistence.GetById<Patient>(id);
    }
}
