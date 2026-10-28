using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Domain.Entities;

public class Patient : EntityBase
{
    public long Dni { get; init; }
    public string? FullName { get; init;}
    public string? UserId { get; private set; }
    public bool IsActive { get; private set; } = false;

    private readonly List<Appointment> _appointments = new List<Appointment>();
    public IReadOnlyCollection<Appointment> Appointments => _appointments.AsReadOnly();
    
    #region Constructor for EF
#pragma warning disable CS8618
    private Patient() { }
#pragma warning restore CS8618
    #endregion

    public Patient(long dni, string fullName, string userId, Guid? id = null):base(id)
    {
        Dni = dni;
        FullName = fullName;
        UserId = userId;
    }
}
