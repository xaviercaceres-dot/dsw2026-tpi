using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Identity;
using Dsw2026Tpi.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dsw2026Tpi.Api.Controllers;

[Route("doctors")]
[Authorize(Policy = Policies.AdminPolicy)]
public class DoctorController : AppController
{
    private readonly IDoctorService _service;

    public DoctorController(IDoctorService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)] 
    [ProducesResponseType(StatusCodes.Status401Unauthorized)] 
    [ProducesResponseType(StatusCodes.Status403Forbidden)] 
    public async Task<IActionResult> GetAll([FromQuery]int pageSize, [FromQuery]int pageIndex, [FromQuery]string? name = null)
    {
        var doctors = await _service.GetAll(pageSize, pageIndex, name);
        return Ok(doctors);
    }

    [HttpGet("{id:guid}/availabilities")] 
    [ProducesResponseType(StatusCodes.Status200OK)] 
    [ProducesResponseType(StatusCodes.Status400BadRequest)] 
    [ProducesResponseType(StatusCodes.Status401Unauthorized)] 
    [ProducesResponseType(StatusCodes.Status403Forbidden)] 
    [ProducesResponseType(StatusCodes.Status404NotFound)] 
    public async Task<IActionResult> GetAvailabilities([FromRoute] Guid id)
    {
        // TODO: Implementar cuando se mergee el PR de disponibilidades
        // var availabilities = await _availabilityService.GetAvailabilitiesForDoctor(id);
        // return Ok(availabilities);

        // Por ahora, devolver lista vacía o manejar el caso donde no exista el módulo aún
        // Si _availabilityService no está disponible, puedes devolver vacío o lanzar una excepción específica
        // Si el ID del doctor es inválido, retornar NotFound (esto lo haría el servicio si lo tuviera)
        // Opcional: Validar si el doctor existe (aunque el servicio de disponibilidad debería hacerlo)
        // var doctorExists = await _service.GetByIdAsync(id) != null;
        // if (!doctorExists) return NotFound();

        // Por ahora, devolvemos una lista vacía como placeholder
        return Ok(Array.Empty<object>()); // Reemplaza object con el tipo real de AvailabilityDto cuando lo tengas
    }

    [HttpGet("{id:guid}")] 
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)] 
    [ProducesResponseType(StatusCodes.Status401Unauthorized)] 
    [ProducesResponseType(StatusCodes.Status403Forbidden)] 
    [ProducesResponseType(StatusCodes.Status404NotFound)] 
    public async Task<IActionResult> GetById([FromRoute] Guid id)
    {
        var doctor = await _service.GetByIdAsync(id);
        if (doctor == null)
        {
            return NotFound(); 
        }
        return Ok(doctor);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)] 
    [ProducesResponseType(StatusCodes.Status401Unauthorized)] 
    [ProducesResponseType(StatusCodes.Status403Forbidden)] 
    [ProducesResponseType(StatusCodes.Status409Conflict)] 
    public async Task<IActionResult> Create([FromBody] DoctorModel.Request request) 
    {
        var doctor = await _service.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = doctor.Id }, doctor);
    }

    [HttpPut("{id:guid}")] 
    [ProducesResponseType(StatusCodes.Status200OK)] 
    [ProducesResponseType(StatusCodes.Status400BadRequest)] 
    [ProducesResponseType(StatusCodes.Status401Unauthorized)] 
    [ProducesResponseType(StatusCodes.Status403Forbidden)] 
    [ProducesResponseType(StatusCodes.Status404NotFound)] 
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] DoctorModel.Request request) 
    {
        var doctor = await _service.UpdateAsync(id, request);
        return Ok(doctor);
    }

    [HttpDelete("{id:guid}")] 
    [ProducesResponseType(StatusCodes.Status204NoContent)] 
    [ProducesResponseType(StatusCodes.Status400BadRequest)] 
    [ProducesResponseType(StatusCodes.Status401Unauthorized)] 
    [ProducesResponseType(StatusCodes.Status403Forbidden)] 
    [ProducesResponseType(StatusCodes.Status404NotFound)] 
    public async Task<IActionResult> Delete([FromRoute] Guid id) 
    {
        await _service.DeleteAsync(id); 
        return NoContent();
    }

}
