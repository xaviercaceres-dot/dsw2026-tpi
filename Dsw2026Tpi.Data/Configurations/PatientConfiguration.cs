using Dsw2026Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Data.Configurations;

public class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("Patients");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Dni)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(p => p.FullName)
            .HasMaxLength(100) 
            .IsRequired();

        builder.Property(p => p.IsActive)
            .HasColumnName("deleted")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(p => p.UserId) 
            .IsRequired(); 

        builder.HasIndex(p => p.Dni).IsUnique();
        builder.HasIndex(p => p.UserId).IsUnique();
    }
}
