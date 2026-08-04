using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.Application.Services;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Data.Identity;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Tests;

public class AuthenticationServiceTests
{
    [Fact]
    public async Task Login_CuandoUsuarioNoExiste_LanzaAuthenticationException()
    {
        var mockUserStore = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(mockUserStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var mockRoleStore = new Mock<IRoleStore<IdentityRole>>();
        var mockRoleManager = new Mock<RoleManager<IdentityRole>>(mockRoleStore.Object, null!, null!, null!, null!);

        var mockSignInService = new Mock<ISignInService>();
        var mockPatientService = new Mock<IPatientService>();
        var mockLogger = new Mock<ILogger<AuthenticationService>>();

        
        mockUserManager.Setup(u => u.FindByEmailAsync(It.IsAny<string>()))
                       .ReturnsAsync((ApplicationUser)null!);

        var authService = new AuthenticationService(
            mockUserManager.Object,
            mockSignInService.Object,
            mockRoleManager.Object,
            null!, 
            mockLogger.Object,
            mockPatientService.Object
        );

        var request = new Dsw2026Tpi.Application.Dtos.LoginAdminModel.Request("admin@test.com", "123");

        await Assert.ThrowsAsync<AuthenticationException>(() =>
            authService.LoginAdmin(request));
    }

    [Fact]
    public async Task GetByIdAsync_CuandoPacienteExiste_DevuelveDatosDelPaciente()
    {
        var mockPersistence = new Mock<IPersistence>();
        var pacienteIdGuid = Guid.NewGuid();

        var pacienteEnBase = new Patient(12345678, "Carlos", "3811234567", "auth-user-123");
        typeof(Patient).GetProperty("Id")?.SetValue(pacienteEnBase, pacienteIdGuid);

        mockPersistence.Setup(p => p.GetById<Patient>(pacienteIdGuid))
                       .ReturnsAsync(pacienteEnBase);

        var patientService = new PatientService(mockPersistence.Object);

        var result = await patientService.GetByIdAsync(pacienteIdGuid);

        Assert.NotNull(result); 
        Assert.Equal(pacienteIdGuid, result.Id);
        Assert.Equal(12345678, result.Dni);

        mockPersistence.Verify(p => p.GetById<Patient>(pacienteIdGuid), Times.Once);
    }
}
