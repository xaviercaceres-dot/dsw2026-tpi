using Dsw2026Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dsw2026Tpi.Data.Configurations;

public class SpecialityConfiguration : IEntityTypeConfiguration<Speciality>
{
    public void Configure(EntityTypeBuilder<Speciality> builder)
    {
        builder.ToTable("Specialities");

        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(s => s.Name).IsUnique();

        builder.Property(s => s.Description).HasMaxLength(300).IsRequired();

        builder.Property(s => s.IsActive)
            .HasColumnName("deleted")
            .HasConversion(isActive => !isActive, deleted => !deleted)
            .HasDefaultValue(true)
            .IsRequired();
    }
}

