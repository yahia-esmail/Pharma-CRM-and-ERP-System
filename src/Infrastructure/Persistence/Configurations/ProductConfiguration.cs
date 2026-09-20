using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.Property(p => p.Sku).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.UnitOfMeasure).HasMaxLength(20).IsRequired();
        builder.Property(p => p.UnitCost).HasColumnType("decimal(18,2)");
        builder.Property(p => p.UnitPrice).HasColumnType("decimal(18,2)");
        builder.Property(p => p.CreatedByUserId).HasMaxLength(450);
        builder.Property(p => p.ModifiedByUserId).HasMaxLength(450);

        builder.HasIndex(p => p.Sku).IsUnique();
    }
}
