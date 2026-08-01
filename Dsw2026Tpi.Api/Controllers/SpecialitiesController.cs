using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Dsw2026Tpi.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SpecialitiesController : ControllerBase
{
    private readonly ISpecialityService _service;

    public SpecialitiesController(ISpecialityService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int pageSize = 10, [FromQuery] int pageIndex = 0, [FromQuery] string? name = null)
    {
        var result = await _service.GetAllAsync(pageSize, pageIndex, name);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(SpecialityModel model)
    {
        var result = await _service.CreateAsync(model);
        return Ok(result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, SpecialityModel model)
    {
        var result = await _service.UpdateAsync(id, model);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return Ok("ok");
    }
}