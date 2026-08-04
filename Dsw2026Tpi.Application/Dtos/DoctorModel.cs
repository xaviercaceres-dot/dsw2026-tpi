namespace Dsw2026Tpi.Application.Dtos;

public record DoctorModel
{
    public record Request(string Name, string LicenseNumber, [property: System.Text.Json.Serialization.JsonPropertyName("specialtyId")] Guid SpecialityId);
    public record Response(Guid Id, string Name, string LicenseNumber, SpecialtyDto? Specialty);
    public record SpecialtyDto(Guid? Id, string? Name);
    public record AvailabilityDto(Guid Id, string Day, string StartTime, string EndTime);
}
