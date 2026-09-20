using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Infrastructure.Persistence.Configurations;

public class CustodyTransactionConfiguration : IEntityTypeConfiguration<CustodyTransaction>
{
    public void Configure(EntityTypeBuilder<CustodyTransaction> builder)
    {
        builder.Property(c => c.ReasonCode).HasMaxLength(50);
        builder.Property(c => c.CreatedByUserId).HasMaxLength(450);
        builder.Property(c => c.ModifiedByUserId).HasMaxLength(450);
        builder.HasIndex(c => c.TransactionDateUtc);
        builder.HasIndex(c => new { c.RepresentativeId, c.ProductId, c.ProductBatchId });

        builder.HasOne(c => c.Representative)
            .WithMany()
            .HasForeignKey(c => c.RepresentativeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.CounterpartRepresentative)
            .WithMany()
            .HasForeignKey(c => c.CounterpartRepresentativeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Product)
            .WithMany()
            .HasForeignKey(c => c.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.ProductBatch)
            .WithMany()
            .HasForeignKey(c => c.ProductBatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.SourceStockMovement)
            .WithMany()
            .HasForeignKey(c => c.SourceStockMovementId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Sale)
            .WithMany()
            .HasForeignKey(c => c.SaleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockReconciliationConfiguration : IEntityTypeConfiguration<StockReconciliation>
{
    public void Configure(EntityTypeBuilder<StockReconciliation> builder)
    {
        builder.Ignore(r => r.Variance);
        builder.Property(r => r.Notes).HasMaxLength(500);
        builder.Property(r => r.CreatedByUserId).HasMaxLength(450);
        builder.Property(r => r.ModifiedByUserId).HasMaxLength(450);
        builder.HasIndex(r => r.ReconciliationDateUtc);

        builder.HasOne(r => r.Representative)
            .WithMany()
            .HasForeignKey(r => r.RepresentativeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Product)
            .WithMany()
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ProductBatch)
            .WithMany()
            .HasForeignKey(r => r.ProductBatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.AdjustmentCustodyTransaction)
            .WithMany()
            .HasForeignKey(r => r.AdjustmentCustodyTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
