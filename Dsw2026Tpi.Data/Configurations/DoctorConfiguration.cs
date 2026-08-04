using Dsw2026Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dsw2026Tpi.Data.Configurations;

public class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> builder)
    {
        builder.ToTable("Doctors");
        
        builder.Property(d => d.Id)
               .HasColumnName("id");

        builder.Property(d => d.Name)
            .HasColumnName("name")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(d => d.LicenseNumber)
            .HasColumnName("license_number")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Property(d => d.SpecialityId)
            .HasColumnName("specialty_id");

        builder.Property(d => d.IsActive)
            .HasColumnName("deleted")
            .HasColumnType("bit")
            .HasConversion(isActive => !isActive, deleted => !deleted)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(d => d.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(d => d.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasOne(d => d.Speciality)
            .WithMany()
            .HasForeignKey(d => d.SpecialityId)
            .HasConstraintName("FK_DOCTORS_SPECIALTY")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
