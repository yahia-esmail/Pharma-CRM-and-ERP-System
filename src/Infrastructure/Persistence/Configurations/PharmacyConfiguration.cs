using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Infrastructure.Persistence.Configurations;

public class PharmacyConfiguration : IEntityTypeConfiguration<Pharmacy>
{
    public void Configure(EntityTypeBuilder<Pharmacy> builder)
    {
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.CreditLimit).HasColumnType("decimal(18,2)");
        builder.Property(p => p.CreatedByUserId).HasMaxLength(450);
        builder.Property(p => p.ModifiedByUserId).HasMaxLength(450);
        builder.HasIndex(p => p.Name);

        builder.HasOne(p => p.PrimaryRepresentative)
            .WithMany()
            .HasForeignKey(p => p.PrimaryRepresentativeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Territory)
            .WithMany()
            .HasForeignKey(p => p.TerritoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PharmacyVisitConfiguration : IEntityTypeConfiguration<PharmacyVisit>
{
    public void Configure(EntityTypeBuilder<PharmacyVisit> builder)
    {
        builder.Property(v => v.CreatedByUserId).HasMaxLength(450);
        builder.Property(v => v.ModifiedByUserId).HasMaxLength(450);
        builder.HasIndex(v => v.VisitDateUtc);

        builder.HasOne(v => v.Pharmacy)
            .WithMany()
            .HasForeignKey(v => v.PharmacyId)
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
