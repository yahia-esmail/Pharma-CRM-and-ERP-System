using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Infrastructure.Persistence.Configurations;

public class VisitPlanConfiguration : IEntityTypeConfiguration<VisitPlan>
{
    public void Configure(EntityTypeBuilder<VisitPlan> builder)
    {
        builder.Property(p => p.CreatedByUserId).HasMaxLength(450);
        builder.Property(p => p.ModifiedByUserId).HasMaxLength(450);
        builder.Property(p => p.ApprovedByUserId).HasMaxLength(450);
        builder.HasIndex(p => new { p.RepresentativeId, p.StartDate, p.EndDate });

        builder.HasOne(p => p.Representative)
            .WithMany()
            .HasForeignKey(p => p.RepresentativeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class VisitPlanItemConfiguration : IEntityTypeConfiguration<VisitPlanItem>
{
    public void Configure(EntityTypeBuilder<VisitPlanItem> builder)
    {
        builder.Property(i => i.CreatedByUserId).HasMaxLength(450);
        builder.Property(i => i.ModifiedByUserId).HasMaxLength(450);
        builder.HasIndex(i => new { i.VisitPlanId, i.PlannedDate });

        builder.HasOne(i => i.VisitPlan)
            .WithMany(p => p.Items)
            .HasForeignKey(i => i.VisitPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Doctor)
            .WithMany()
            .HasForeignKey(i => i.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Pharmacy)
            .WithMany()
            .HasForeignKey(i => i.PharmacyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
