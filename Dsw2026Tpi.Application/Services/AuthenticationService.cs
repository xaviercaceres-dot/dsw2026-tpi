using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.CrossCutting.Helpers;
using Dsw2026Tpi.CrossCutting.Identity;
using Dsw2026Tpi.CrossCutting.Resources;
using Dsw2026Tpi.Data.Identity;
using Dsw2026Tpi.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Dsw2026Tpi.Application.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISignInService _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly JwtService _jwtService;
    private readonly ILogger<AuthenticationService> _logger;
    private readonly IPatientService _patientService;

    public AuthenticationService(UserManager<ApplicationUser> userManager,
        ISignInService signInManager,
        RoleManager<IdentityRole> roleManager,
        JwtService jwtService,
        ILogger<AuthenticationService> logger,
        IPatientService patientService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _jwtService = jwtService;
        _logger = logger;
        _patientService = patientService;
    }

    public async Task<LoginAdminModel.Response> LoginAdmin(LoginAdminModel.Request request)
    {
        var ex = new ValidationException();
        if (!request.Email.IsEmailValid())
            ex.WithDetail("email", "El email es obligatorio y debe tener un formato válido.");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            ex.WithDetail("password", "La contraseña es obligatoria y debe tener un mínimo de 8 caracteres.");
        if (ex.Error.Details.Count > 0) throw ex;

        var user = await _userManager.FindByEmailAsync(request.Email) ?? throw new AuthenticationException();
        var isPaswordValid = await _userManager.CheckPasswordAsync(user, request.Password);

        if (!isPaswordValid)
        {
            _logger.LogWarning("Intento de login con contraseña incorrecta para: {Email}", request.Email);
            throw new AuthenticationException();
        }

        var roles = (await _userManager.GetRolesAsync(user));

        if (!roles.Contains(Roles.Administrator))
        {
            _logger.LogWarning("Intento de acceso denegado. Usuario sin rol Admin intentó loguearse en portal administrativo: {Email}", request.Email);
            throw new AuthenticationException();
        }

        var role = Roles.Administrator;
        var token = _jwtService.GenerateToken(user.UserName!, role);

        return new LoginAdminModel.Response(
            token,
            role
        );
    }

    public async Task<LoginPatientModel.Response> LoginPatient(LoginPatientModel.Request request)
    {
        var ex = new ValidationException();
        if (!request.Email.IsEmailValid())
            ex.WithDetail("email", "El email es obligatorio y debe tener un formato válido.");
        if (!request.Dni.IsDniValid())
            ex.WithDetail("dni", "El DNI es obligatorio y debe tener 7 u 8 dígitos.");
        if (ex.Error.Details.Count > 0) throw ex;

        var paciente = await _patientService.GetByDniAsync(request.Dni);
        ApplicationUser? user = null;

        if (paciente == null)
        {
            user = await _userManager.FindByEmailAsync(request.Email);

            if (user == null)
            {
                string tempPassword = GenerateRandomPassword();
                user = new ApplicationUser
                {
                    UserName = request.Email,
                    Email = request.Email,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, tempPassword);
                if (!result.Succeeded)
                {
                    _logger.LogError("Error al crear usuario para paciente: {Errors}", result.Errors);
                    throw new ConflictException(nameof(ErrorCodes.REGISTER_USER_CONFLICT), ErrorCodes.REGISTER_USER_CONFLICT);
                }

                var roleResult = await _userManager.AddToRoleAsync(user, Roles.Patient);
                if (!roleResult.Succeeded)
                {
                    _logger.LogError("Error al asignar rol a usuario para paciente: {Errors}", roleResult.Errors);
                    throw new ConflictException(nameof(ErrorCodes.REGISTER_USER_CONFLICT), ErrorCodes.REGISTER_USER_CONFLICT);
                }
            }
            else
            {
                var rolesUser = await _userManager.GetRolesAsync(user);
                if (!rolesUser.Contains(Roles.Patient))
                {
                    _logger.LogWarning("Intento de login de paciente con email existente pero sin rol PACIENTE: {Email}", request.Email);
                    throw new ConflictException(ErrorCodes.REGISTER_USER_INVALID, nameof(ErrorCodes.REGISTER_USER_INVALID));
                }
            }

            var name = ExtractNameFromEmail(request.Email);
            paciente = new Patient(request.Dni, name, "00000000", user.Id);
            await _patientService.AddAsync(paciente);
        }
        else
        {
            if (string.IsNullOrEmpty(paciente.UserId)) throw new AuthenticationException();

            user = await _userManager.FindByIdAsync(paciente.UserId) ?? throw new AuthenticationException();
            if (user == null || !user.Email!.Equals(request.Email, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Paciente encontrado con DNI {Dni}, pero no existe un usuario con email {Email}", request.Dni, request.Email);
                throw new AuthenticationException()
                    .WithDetail("Credentials", "Las credenciales proporcionadas no son válidas.");
            }

            var roles = (await _userManager.GetRolesAsync(user));
            if (!roles.Contains(Roles.Patient))
            {
                _logger.LogWarning("Intento de login de paciente con email existente pero sin rol PACIENTE: {Email}", request.Email);
                throw new ConflictException(ErrorCodes.REGISTER_USER_INVALID, nameof(ErrorCodes.REGISTER_USER_INVALID));
            }
        }

        var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? Roles.Patient;
        var token = _jwtService.GenerateToken(user.UserName!, role);

        return new LoginPatientModel.Response(
            token,
            role
        );
    }

    public async Task<RegisterModel.Response> Register(RegisterModel.Request request)
    {
        if (!request.Email.IsEmailValid()) throw new ValidationException(ErrorCodes.REGISTER_USER_INVALID,
            nameof(ErrorCodes.REGISTER_USER_INVALID));

        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
            throw new ConflictException(ErrorCodes.REGISTER_USER_CONFLICT, "El correo electrónico ya se encuentra registrado.");

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded) throw new ValidationException(ErrorCodes.REGISTER_USER_INVALID,
            "Los datos proporcionados no cumplen con las políticas de seguridad.")
                .WithDetail(result.Errors.Select(e => (e.Code, e.Description)));

        var roleResult = await _userManager.AddToRoleAsync(user, Roles.Administrator);
        if (!roleResult.Succeeded)
        {
            _logger.LogError("Error crítico al asignar el rol {Role} al usuario {Email}. Errores: {Errors}", Roles.Administrator, request.Email, roleResult.Errors);
            throw new ConflictException(ErrorCodes.REGISTER_USER_CONFLICT, "Ocurrió un problema al configurar los permisos del administrador.");
        }

        _logger.LogInformation("Usuario registrado: {Email}", request.Email);

        return new RegisterModel.Response(request.Email);
    }

    private string GenerateRandomPassword()
    {
        return $"Temp_{Guid.NewGuid():N}".Substring(0, 12);
    }

    private string ExtractNameFromEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex > 0)
        {
            var namePart = email.Substring(0, atIndex);
            var firstName = namePart.Split('.')[0];
            if (firstName.Length > 0)
            {
                return char.ToUpper(firstName[0]) + firstName.Substring(1).ToLower();
            }
        }
        return "Paciente";
    }
}

