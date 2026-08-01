using Dsw2026Tpi.Application.Dtos;

namespace Dsw2026Tpi.Application.Interfaces;

public interface ISpecialityService
{
    Task<IEnumerable<SpecialityModel>> GetAllAsync(int pageSize, int pageIndex, string? name);
    Task<SpecialityModel> CreateAsync(SpecialityModel model);
    Task<SpecialityModel> UpdateAsync(Guid id, SpecialityModel model);
    Task DeleteAsync(Guid id);
}