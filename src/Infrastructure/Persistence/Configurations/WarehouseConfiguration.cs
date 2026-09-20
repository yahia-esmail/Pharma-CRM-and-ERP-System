using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Infrastructure.Persistence.Configurations;

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.Property(w => w.Name).HasMaxLength(150).IsRequired();
        builder.Property(w => w.ResponsibleUserId).HasMaxLength(450);
        builder.Property(w => w.CreatedByUserId).HasMaxLength(450);
        builder.Property(w => w.ModifiedByUserId).HasMaxLength(450);
        builder.HasIndex(w => w.Name);
    }
}

public class ProductBatchConfiguration : IEntityTypeConfiguration<ProductBatch>
{
    public void Configure(EntityTypeBuilder<ProductBatch> builder)
    {
        builder.Property(b => b.BatchNumber).HasMaxLength(50).IsRequired();
        builder.Property(b => b.CreatedByUserId).HasMaxLength(450);
        builder.Property(b => b.ModifiedByUserId).HasMaxLength(450);
        builder.HasIndex(b => new { b.ProductId, b.BatchNumber }).IsUnique();
        builder.HasIndex(b => b.ExpiryDate);

        builder.HasOne(b => b.Product)
            .WithMany()
            .HasForeignKey(b => b.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
