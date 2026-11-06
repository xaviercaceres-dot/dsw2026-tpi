using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.CrossCutting.Models;
using Dsw2026Tpi.CrossCutting.Resources;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

namespace Dsw2026Tpi.Api.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Se produjo un error durante el procesamiento de la solicitud");
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        ErrorResponse error = ex is AppException exApp ?
            exApp.Error :
            new ErrorResponse(nameof(ErrorCodes.UNHANDLED_ERROR), ErrorCodes.UNHANDLED_ERROR);
        var status = ex switch
        {
            ValidationException => HttpStatusCode.BadRequest,
            BusinessRuleException => HttpStatusCode.BadRequest,
            EntityNotFoundException => HttpStatusCode.NotFound,
            ConflictException => HttpStatusCode.Conflict,
            AuthenticationException => HttpStatusCode.Unauthorized,
            AuthorizationException => HttpStatusCode.Forbidden,
            _ => HttpStatusCode.InternalServerError,
        };
        var result = JsonSerializer.Serialize(error, SerializerOptions);
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)status;
        await context.Response.WriteAsync(result);
    }

    /// <summary>
    /// Respuesta 400 con el formato de error de la API cuando el modelo de entrada no se pudo interpretar
    /// (JSON mal formado, tipos o formatos inválidos en el cuerpo, la ruta o los query params).
    /// </summary>
    public static IActionResult InvalidModelStateResponse(ActionContext context)
    {
        var error = new ErrorResponse(nameof(ErrorCodes.VALIDATION_ERROR), ErrorCodes.VALIDATION_ERROR);
        foreach (var (key, entry) in context.ModelState)
        {
            if (entry.Errors.Count == 0) continue;

            // Las claves de System.Text.Json tienen la forma "$.campo"; "$" o "" se refieren al cuerpo completo.
            var field = key.StartsWith("$.") ? key[2..] : key is "$" or "" ? "body" : key;
            var issue = field == "body"
                ? "El cuerpo de la solicitud es obligatorio y debe ser un JSON válido."
                : "El valor no tiene un formato válido.";
            error.AddDetail(field, issue);
        }
        return new BadRequestObjectResult(error);
    }

    /// <summary>
    /// Completa con el formato de error de la API las respuestas de error que no tienen cuerpo
    /// (ruta inexistente, método no permitido, tipo de contenido no soportado, etc.).
    /// </summary>
    public static async Task WriteStatusCodeResponseAsync(StatusCodeContext context)
    {
        var response = context.HttpContext.Response;
        var error = response.StatusCode switch
        {
            StatusCodes.Status401Unauthorized => new ErrorResponse(nameof(ErrorCodes.AUTHENTICATION_FAILED), ErrorCodes.AUTHENTICATION_FAILED),
            StatusCodes.Status403Forbidden => new ErrorResponse(nameof(ErrorCodes.AUTHORIZATION_FAILED), ErrorCodes.AUTHORIZATION_FAILED),
            StatusCodes.Status404NotFound => new ErrorResponse(nameof(ErrorCodes.RESOURCE_NOT_FOUND), ErrorCodes.RESOURCE_NOT_FOUND),
            StatusCodes.Status405MethodNotAllowed => new ErrorResponse(nameof(ErrorCodes.METHOD_NOT_ALLOWED), ErrorCodes.METHOD_NOT_ALLOWED),
            StatusCodes.Status415UnsupportedMediaType => new ErrorResponse(nameof(ErrorCodes.UNSUPPORTED_MEDIA_TYPE), ErrorCodes.UNSUPPORTED_MEDIA_TYPE),
            >= StatusCodes.Status500InternalServerError => new ErrorResponse(nameof(ErrorCodes.UNHANDLED_ERROR), ErrorCodes.UNHANDLED_ERROR),
            _ => new ErrorResponse(nameof(ErrorCodes.VALIDATION_ERROR), ErrorCodes.VALIDATION_ERROR),
        };
        response.ContentType = "application/json";
        await response.WriteAsync(JsonSerializer.Serialize(error, SerializerOptions));
    }
}
