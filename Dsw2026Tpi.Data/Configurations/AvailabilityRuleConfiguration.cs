using Dsw2026Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Data.Configurations
{
    public class AvailabilityRuleConfiguration : IEntityTypeConfiguration<AvailabilityRule>

    {
        public void Configure(EntityTypeBuilder<AvailabilityRule> builder)
        {
            builder.ToTable("AvailabilityRules");

            builder.Property(r => r.DayOfWeek).HasMaxLength(20).IsRequired();

            builder.HasOne(r => r.Doctor)
                .WithMany(d => d.AvailabilityRules)
                .HasForeignKey(r => r.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(r => new { r.DoctorId, r.Year, r.Month, r.DayOfWeek });
        }
    }
}
