using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Infrastructure.Persistence.Configurations;

public class ReturnTransactionConfiguration : IEntityTypeConfiguration<ReturnTransaction>
{
    public void Configure(EntityTypeBuilder<ReturnTransaction> builder)
    {
        builder.Property(r => r.Notes).HasMaxLength(500);
        builder.Property(r => r.RejectionReason).HasMaxLength(500);
        builder.Property(r => r.RequestedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(r => r.ApprovedByUserId).HasMaxLength(450);
        builder.Property(r => r.CreatedByUserId).HasMaxLength(450);
        builder.Property(r => r.ModifiedByUserId).HasMaxLength(450);
        builder.HasIndex(r => new { r.RepresentativeId, r.Status });

        builder.HasOne(r => r.Representative)
            .WithMany()
            .HasForeignKey(r => r.RepresentativeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Pharmacy)
            .WithMany()
            .HasForeignKey(r => r.PharmacyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Warehouse)
            .WithMany()
            .HasForeignKey(r => r.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Product)
            .WithMany()
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ProductBatch)
            .WithMany()
            .HasForeignKey(r => r.ProductBatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
