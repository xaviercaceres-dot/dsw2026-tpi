using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Data;
using Microsoft.EntityFrameworkCore;

namespace Dsw2026Tpi.Application.Services;

public class SpecialityService : ISpecialityService
{
    private readonly Dsw2026TpiDbContext _context;

    public SpecialityService(Dsw2026TpiDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<SpecialityModel>> GetAllAsync(int pageSize, int pageIndex, string? name)
    {
        var query = _context.Specialities.Where(s => !s.Deleted);

        if (!string.IsNullOrEmpty(name))
        {
            query = query.Where(s => s.Name.Contains(name));
        }

        return await query.Skip(pageIndex * pageSize)
            .Take(pageSize)
            .Select(s => new SpecialityModel { Id = s.Id, Name = s.Name, Description = s.Description })
            .ToListAsync();
    }

    public async Task<SpecialityModel> CreateAsync(SpecialityModel model)
    {
        var speciality = new Speciality(model.Name!, model.Description!);
        _context.Specialities.Add(speciality);
        await _context.SaveChangesAsync();

        return new SpecialityModel { Id = speciality.Id, Name = speciality.Name, Description = speciality.Description };
    }

    public async Task<SpecialityModel> UpdateAsync(Guid id, SpecialityModel model)
    {
        var speciality = await _context.Specialities.FirstOrDefaultAsync(s => s.Id == id && !s.Deleted);
        if (speciality == null) throw new Exception("Speciality not found");

        var updatedSpeciality = new Speciality(model.Name!, model.Description!, id);
        _context.Entry(speciality).CurrentValues.SetValues(updatedSpeciality);
        await _context.SaveChangesAsync();

        return new SpecialityModel { Id = speciality.Id, Name = speciality.Name, Description = speciality.Description };
    }

    public async Task DeleteAsync(Guid id)
    {
        var speciality = await _context.Specialities.FirstOrDefaultAsync(s => s.Id == id && !s.Deleted);
        if (speciality == null) throw new Exception("Speciality not found");

        speciality.GetType().GetProperty("Deleted")?.SetValue(speciality, true);
        await _context.SaveChangesAsync();
    }
}