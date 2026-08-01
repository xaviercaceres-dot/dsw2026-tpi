namespace Dsw2026Tpi.Application.Dtos;

public record DoctorModel
{
    public record Request(string Name, string LicenseNumber, Guid SpecialityId);
    public record Response(Guid Id, string Name, string LicenseNumber, SpecialtyDto? Specialty);
    public record SpecialtyDto(Guid? SpecialityId, string? Name);
    public record AvailabilityDto(string Day, string StartTime, string EndTime);
}
