using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Infrastructure.Persistence.Configurations;

public class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> builder)
    {
        builder.Property(d => d.FullName).HasMaxLength(200).IsRequired();
        builder.Property(d => d.Specialty).HasMaxLength(150).IsRequired();
        builder.Property(d => d.CreatedByUserId).HasMaxLength(450);
        builder.Property(d => d.ModifiedByUserId).HasMaxLength(450);
        builder.HasIndex(d => d.FullName);
        builder.HasIndex(d => new { d.Phone, d.ClinicOrHospital });

        builder.HasOne(d => d.Classification)
            .WithMany(c => c.Doctors)
            .HasForeignKey(d => d.ClassificationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(d => d.PrimaryRepresentative)
            .WithMany(r => r.PrimaryDoctors)
            .HasForeignKey(d => d.PrimaryRepresentativeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Territory)
            .WithMany(t => t.Doctors)
            .HasForeignKey(d => d.TerritoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class DoctorClassificationConfiguration : IEntityTypeConfiguration<DoctorClassification>
{
    public void Configure(EntityTypeBuilder<DoctorClassification> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.Property(c => c.CreatedByUserId).HasMaxLength(450);
        builder.Property(c => c.ModifiedByUserId).HasMaxLength(450);
    }
}

public class DoctorVisitConfiguration : IEntityTypeConfiguration<DoctorVisit>
{
    public void Configure(EntityTypeBuilder<DoctorVisit> builder)
    {
        builder.Property(v => v.CreatedByUserId).HasMaxLength(450);
        builder.Property(v => v.ModifiedByUserId).HasMaxLength(450);
        builder.HasIndex(v => v.VisitDateUtc);

        builder.HasOne(v => v.Doctor)
            .WithMany(d => d.Visits)
            .HasForeignKey(v => v.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.Representative)
            .WithMany()
            .HasForeignKey(v => v.RepresentativeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<VisitPlanItem>()
            .WithMany()
            .HasForeignKey(v => v.VisitPlanItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class DoctorFollowUpConfiguration : IEntityTypeConfiguration<DoctorFollowUp>
{
    public void Configure(EntityTypeBuilder<DoctorFollowUp> builder)
    {
        builder.Property(f => f.CreatedByUserId).HasMaxLength(450);
        builder.Property(f => f.ModifiedByUserId).HasMaxLength(450);

        builder.HasOne(f => f.Doctor)
            .WithMany(d => d.FollowUps)
            .HasForeignKey(f => f.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.DoctorVisit)
            .WithMany()
            .HasForeignKey(f => f.DoctorVisitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.OwnerRepresentative)
            .WithMany()
            .HasForeignKey(f => f.OwnerRepresentativeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
