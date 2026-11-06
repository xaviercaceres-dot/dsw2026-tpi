using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Dsw2026Tpi.Api.Controllers;

/// <summary>
/// Clase base para configuraciones generales de controladores
/// </summary>
[ApiController]
[Route("api")]
public abstract class AppController : ControllerBase
{
    /// <summary>Id del usuario autenticado (claim "sub" del JWT).</summary>
    protected string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
}

