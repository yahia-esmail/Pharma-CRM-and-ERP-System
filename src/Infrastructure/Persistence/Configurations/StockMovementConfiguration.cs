using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Infrastructure.Persistence.Configurations;

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.Property(m => m.ReasonCode).HasMaxLength(50);
        builder.Property(m => m.ReferenceNote).HasMaxLength(200);
        builder.Property(m => m.CreatedByUserId).HasMaxLength(450);
        builder.Property(m => m.ModifiedByUserId).HasMaxLength(450);
        builder.HasIndex(m => m.MovementDateUtc);
        builder.HasIndex(m => new { m.ProductId, m.ProductBatchId });

        builder.HasOne(m => m.Product)
            .WithMany()
            .HasForeignKey(m => m.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.ProductBatch)
            .WithMany()
            .HasForeignKey(m => m.ProductBatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.SourceWarehouse)
            .WithMany()
            .HasForeignKey(m => m.SourceWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.DestinationWarehouse)
            .WithMany()
            .HasForeignKey(m => m.DestinationWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Representative)
            .WithMany()
            .HasForeignKey(m => m.RepresentativeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
