using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Infrastructure.Persistence.Configurations;

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.Property(o => o.CreatedByUserId).HasMaxLength(450);
        builder.Property(o => o.ModifiedByUserId).HasMaxLength(450);
        builder.HasIndex(o => o.OrderDateUtc);

        builder.HasOne(o => o.Supplier)
            .WithMany()
            .HasForeignKey(o => o.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        builder.Property(l => l.UnitCost).HasColumnType("decimal(18,2)");
        builder.Property(l => l.CreatedByUserId).HasMaxLength(450);
        builder.Property(l => l.ModifiedByUserId).HasMaxLength(450);

        builder.HasOne(l => l.PurchaseOrder)
            .WithMany(o => o.Lines)
            .HasForeignKey(l => l.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Product)
            .WithMany()
            .HasForeignKey(l => l.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchaseReceiptConfiguration : IEntityTypeConfiguration<PurchaseReceipt>
{
    public void Configure(EntityTypeBuilder<PurchaseReceipt> builder)
    {
        builder.Property(r => r.CreatedByUserId).HasMaxLength(450);
        builder.Property(r => r.ModifiedByUserId).HasMaxLength(450);
        builder.HasIndex(r => r.ReceiptDateUtc);

        builder.HasOne(r => r.PurchaseOrder)
            .WithMany()
            .HasForeignKey(r => r.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Warehouse)
            .WithMany()
            .HasForeignKey(r => r.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchaseReceiptLineConfiguration : IEntityTypeConfiguration<PurchaseReceiptLine>
{
    public void Configure(EntityTypeBuilder<PurchaseReceiptLine> builder)
    {
        builder.Property(l => l.CreatedByUserId).HasMaxLength(450);
        builder.Property(l => l.ModifiedByUserId).HasMaxLength(450);

        builder.HasOne(l => l.PurchaseReceipt)
            .WithMany(r => r.Lines)
            .HasForeignKey(l => l.PurchaseReceiptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.PurchaseOrderLine)
            .WithMany()
            .HasForeignKey(l => l.PurchaseOrderLineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Product)
            .WithMany()
            .HasForeignKey(l => l.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.ProductBatch)
            .WithMany()
            .HasForeignKey(l => l.ProductBatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.StockMovement)
            .WithMany()
            .HasForeignKey(l => l.StockMovementId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
