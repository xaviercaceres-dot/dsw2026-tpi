using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Domain.Entities;

public class Patient : EntityBase
{
    public long Dni { get; init; }
    public string? Name { get; init;}
    public string? PhoneNumber { get; init;}
    public string? UserId { get; private set; }

    private readonly List<Appointment> _appointments = new List<Appointment>();
    public IReadOnlyCollection<Appointment> Appointments => _appointments.AsReadOnly();
    
    #region Constructor for EF
#pragma warning disable CS8618
    private Patient() { }
#pragma warning restore CS8618
    #endregion

    public Patient(long dni, string name, string phoneNumber, string userId)
    {
        Dni = dni;
        Name = name;
        PhoneNumber = phoneNumber;
        UserId = userId;
    }
}
