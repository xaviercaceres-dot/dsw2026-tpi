using System.Text.Json.Serialization;

namespace Dsw2026Tpi.Domain.Entities;

public class Speciality : EntityBase
{
    [JsonInclude]
    public string ?Name { get; private set; }
    [JsonInclude]
    public string ?Description { get; private set; }

    public bool IsActive { get; private set; }

    private readonly List<Doctor> _doctors = new List<Doctor>();
    public IReadOnlyCollection<Doctor> Doctors => _doctors.AsReadOnly();

    #region Constructor for EF
#pragma warning disable CS8618
    public Speciality() { }
#pragma warning restore CS8618
    #endregion

    
    public Speciality(string name, string description, Guid? id = null) : base(id)
    {
        Name = name;
        Description = description;
        IsActive = true;
    }
    public void Update(string name, string description)
    {
        Name = name;
        Description = description;
    }

    public void Deactivate() => IsActive = false;

}
