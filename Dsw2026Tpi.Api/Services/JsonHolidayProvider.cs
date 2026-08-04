using Dsw2026Tpi.Application.Interfaces;
using System.Text.Json;

namespace Dsw2026Tpi.Api.Services
{
    public class JsonHolidayProvider : IHolidayProvider
    {
        private readonly string _path;
        public JsonHolidayProvider(IWebHostEnvironment env) => _path = Path.Combine(env.ContentRootPath, "holidays.json");

        public async Task<HashSet<DateTime>> GetHolidaysAsync()
        {
            if (!File.Exists(_path)) return new HashSet<DateTime>();
            var json = await File.ReadAllTextAsync(_path);
            var dates = JsonSerializer.Deserialize<List<DateOnly>>(json) ?? new List<DateOnly>();
            return dates.Select(d => d.ToDateTime(TimeOnly.MinValue).Date).ToHashSet();
        }
    }
}
