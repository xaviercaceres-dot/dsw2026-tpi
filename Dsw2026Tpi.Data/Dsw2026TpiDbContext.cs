using Dsw2026Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Dsw2026Tpi.Data;

public class Dsw2026TpiDbContext: DbContext
{
    public DbSet<Patient> Patients { get; set; }

    public DbSet<Speciality> Specialities { get; set; }

    public Dsw2026TpiDbContext(DbContextOptions<Dsw2026TpiDbContext> options):
        base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        modelBuilder.Entity<Speciality>().HasData(
        new Speciality { Id = Guid.Parse("d12a12d1-1234-4321-abcd-1234567890ab"), Name = "Odontología", Description = "Atención dental" },
        new Speciality { Id = Guid.Parse("f23b23f2-5678-8765-bcde-2345678901bc"), Name = "Pediatría", Description = "Atención infantil" }
    );
    }

  

}
