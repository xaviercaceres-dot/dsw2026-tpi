using Dsw2026Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Data.Configurations;

public class AppoimentConfiguration  : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments");

        builder.Property(a => a.Reason).HasMaxLength(300).IsRequired();
        builder.Property(a => a.Status).HasMaxLength(20).HasConversion<string>().IsRequired();
    

        builder.HasOne(a => a.AvailabilitySlot)
            .WithOne(s => s.Appointment)
            .HasForeignKey<Appointment>(a => a.AvailabilitySlotId)
            .OnDelete(DeleteBehavior.Restrict);

    

        builder.HasOne(a => a.Patient)
            .WithMany(p => p.Appointments)
            .HasForeignKey(a => a.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.AvailabilitySlotId, a.Status });
        builder.HasIndex(a => a.AvailabilitySlotId).IsUnique();
    }

}
