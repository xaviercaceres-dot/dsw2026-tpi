using Dsw2026Tpi.CrossCutting.Identity;
using Dsw2026Tpi.Data.Identity;
using Microsoft.AspNetCore.Identity;

namespace Dsw2026Tpi.Api.Configurations;

public static class SeedConfigurationExtensions
{
    public static async Task SeedAdminUserAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        // Roles del enunciado (ADMINISTRADOR / PACIENTE). En bases creadas con los nombres anteriores
        // ("Administrador" / "Paciente", mismo nombre normalizado) se actualiza el nombre visible.
        foreach (var roleName in new[] { Roles.Administrator, Roles.Patient })
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
            else if (role.Name != roleName)
            {
                await roleManager.SetRoleNameAsync(role, roleName);
                await roleManager.UpdateAsync(role);
            }
        }

        var adminEmail = config["Admin:Email"] ?? "admin@system.com";

        if (await userManager.FindByEmailAsync(adminEmail) is not null) return;

        // La contraseña inicial del administrador se toma de la configuración (User Secrets o variables de entorno),
        // nunca del código fuente.
        var adminPassword = config["Admin:Password"];
        if (string.IsNullOrWhiteSpace(adminPassword))
            throw new InvalidOperationException("Falta configurar 'Admin:Password' para crear el administrador inicial. En desarrollo: dotnet user-secrets set \"Admin:Password\" \"<contraseña>\" (ver README).");

        var admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(admin, adminPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException("No se pudo crear el administrador inicial: " +
                string.Join(" ", result.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(admin, Roles.Administrator);
    }
}
