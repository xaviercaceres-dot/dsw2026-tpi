using Dsw2026Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Data.Configurations
{
    public  class AvailabilitySlotConfiguration : IEntityTypeConfiguration<AvailabilitySlot>
    {
        public void Configure(EntityTypeBuilder<AvailabilitySlot> builder)
        {
            builder.ToTable("AvailabilitySlots");

            builder.Property(s => s.Status).HasMaxLength(20).IsRequired();

            builder.HasOne(s => s.AvailabilityRule)
                .WithMany()
                .HasForeignKey(s => s.AvailabilityRuleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(s => new { s.DoctorId, s.SlotDate, s.StartTime });
          
            builder.Property<byte[]>("RowVersion").IsRowVersion();

    
        }

    }
}
