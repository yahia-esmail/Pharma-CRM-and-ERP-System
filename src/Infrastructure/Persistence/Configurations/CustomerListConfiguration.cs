using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Infrastructure.Persistence.Configurations;

public class CustomerListConfiguration : IEntityTypeConfiguration<CustomerList>
{
    public void Configure(EntityTypeBuilder<CustomerList> builder)
    {
        builder.Property(l => l.Name).HasMaxLength(150).IsRequired();
        builder.Property(l => l.FilterSegment).HasMaxLength(50);
        builder.Property(l => l.CreatedByUserId).HasMaxLength(450);
        builder.Property(l => l.ModifiedByUserId).HasMaxLength(450);
        builder.HasIndex(l => l.Name);

        builder.HasOne(l => l.OwnerRepresentative)
            .WithMany()
            .HasForeignKey(l => l.OwnerRepresentativeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.FilterTerritory)
            .WithMany()
            .HasForeignKey(l => l.FilterTerritoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.FilterClassification)
            .WithMany()
            .HasForeignKey(l => l.FilterClassificationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CustomerListItemConfiguration : IEntityTypeConfiguration<CustomerListItem>
{
    public void Configure(EntityTypeBuilder<CustomerListItem> builder)
    {
        builder.Property(i => i.CreatedByUserId).HasMaxLength(450);
        builder.Property(i => i.ModifiedByUserId).HasMaxLength(450);
        builder.Property(i => i.AddedByUserId).HasMaxLength(450).IsRequired();

        builder.HasOne(i => i.CustomerList)
            .WithMany(l => l.Items)
            .HasForeignKey(i => i.CustomerListId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Doctor)
            .WithMany()
            .HasForeignKey(i => i.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Pharmacy)
            .WithMany()
            .HasForeignKey(i => i.PharmacyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.CustomerListId, i.DoctorId }).IsUnique().HasFilter("[DoctorId] IS NOT NULL");
        builder.HasIndex(i => new { i.CustomerListId, i.PharmacyId }).IsUnique().HasFilter("[PharmacyId] IS NOT NULL");
    }
}

public class CustomerTransferLogConfiguration : IEntityTypeConfiguration<CustomerTransferLog>
{
    public void Configure(EntityTypeBuilder<CustomerTransferLog> builder)
    {
        builder.Property(t => t.Reason).HasMaxLength(500);
        builder.Property(t => t.CreatedByUserId).HasMaxLength(450);
        builder.Property(t => t.ModifiedByUserId).HasMaxLength(450);
        builder.Property(t => t.ApprovedByUserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(t => t.TransferDateUtc);

        builder.HasOne(t => t.Doctor)
            .WithMany()
            .HasForeignKey(t => t.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Pharmacy)
            .WithMany()
            .HasForeignKey(t => t.PharmacyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.FromRepresentative)
            .WithMany()
            .HasForeignKey(t => t.FromRepresentativeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.ToRepresentative)
            .WithMany()
            .HasForeignKey(t => t.ToRepresentativeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
