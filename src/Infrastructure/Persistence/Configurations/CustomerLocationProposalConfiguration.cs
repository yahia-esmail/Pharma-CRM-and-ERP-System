using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Infrastructure.Persistence.Configurations;

public class CustomerLocationProposalConfiguration : IEntityTypeConfiguration<CustomerLocationProposal>
{
    public void Configure(EntityTypeBuilder<CustomerLocationProposal> builder)
    {
        builder.Property(p => p.Note).HasMaxLength(500);
        builder.Property(p => p.ReviewNote).HasMaxLength(500);
        builder.Property(p => p.ReviewedByUserId).HasMaxLength(450);

        builder.ToTable(t => t.HasCheckConstraint("CK_CustomerLocationProposals_OneCustomer",
            "([DoctorId] IS NOT NULL AND [PharmacyId] IS NULL) OR ([DoctorId] IS NULL AND [PharmacyId] IS NOT NULL)"));

        builder.HasIndex(p => p.Status);

        // Restrict, not cascade: soft-deleted customers keep their history, and SQL Server rejects multiple
        // cascade paths through Representative anyway.
        builder.HasOne(p => p.Doctor).WithMany().HasForeignKey(p => p.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Pharmacy).WithMany().HasForeignKey(p => p.PharmacyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Representative).WithMany().HasForeignKey(p => p.RepresentativeId).OnDelete(DeleteBehavior.Restrict);
    }
}
