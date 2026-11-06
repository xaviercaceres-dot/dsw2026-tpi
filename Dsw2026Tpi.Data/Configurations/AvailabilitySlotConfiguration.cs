using Dsw2026Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Data.Configurations;

public  class AvailabilitySlotConfiguration : IEntityTypeConfiguration<AvailabilitySlot>
{
    public void Configure(EntityTypeBuilder<AvailabilitySlot> builder)
    {
        builder.ToTable("AvailabilitySlots");

        builder.Property(s => s.Status).HasMaxLength(20).HasConversion<string>().IsRequired();

        builder.HasOne(s => s.AvailabilityRule)
            .WithMany(r => r.AvailabilitySlots)
            .HasForeignKey(s => s.AvailabilityRuleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Modelo de datos: un único turno por fecha y hora de inicio dentro de cada disponibilidad.
        builder.HasIndex(s => new { s.AvailabilityRuleId, s.SlotDate, s.StartTime }).IsUnique();
      
        builder.Property<byte[]>("RowVersion").IsRowVersion();


    }

}
