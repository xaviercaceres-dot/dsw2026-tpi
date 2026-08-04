using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Moq;
using Dsw2026Tpi.Application.Services;

namespace Dsw2026Tpi.Tests;

public class DoctorServiceTests
{
    [Fact]
    public async Task DeleteAsync_CuandoElDoctorNoExiste_LanzaEntityNotFoundException()
    {
        var mockPersistence = new Mock<IPersistence>();
        var doctorId = Guid.NewGuid();

        mockPersistence.Setup(p => p.GetById<Doctor>(doctorId, It.IsAny<string[]>()))
                        .ReturnsAsync((Doctor)null!);

        var doctorService = new DoctorService(mockPersistence.Object);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            doctorService.DeleteAsync(doctorId));
    }

    [Fact]
    public async Task DeleteAsync_CuandoElDoctorExiste_RealizaBajaLogica()
    {
        var mockPersistence = new Mock<IPersistence>();
        var doctorId = Guid.NewGuid();
        var existingDoctor = new Doctor("Dr. Test", "LIC001", null!, doctorId);

        mockPersistence.Setup(p => p.GetById<Doctor>(doctorId, It.IsAny<string[]>()))
                       .ReturnsAsync(existingDoctor);

        var doctorService = new DoctorService(mockPersistence.Object);

        await doctorService.DeleteAsync(doctorId);

        Assert.False(existingDoctor.IsActive);
        mockPersistence.Verify(p => p.Update(existingDoctor), Times.Once);
    }
}
