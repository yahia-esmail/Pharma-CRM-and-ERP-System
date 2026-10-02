using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.Property(o => o.CreatedByUserId).HasMaxLength(450);
        builder.Property(o => o.ModifiedByUserId).HasMaxLength(450);
        builder.HasIndex(o => o.OrderDateUtc);

        builder.HasOne(o => o.Pharmacy)
            .WithMany()
            .HasForeignKey(o => o.PharmacyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Representative)
            .WithMany()
            .HasForeignKey(o => o.RepresentativeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.PharmacyVisit)
            .WithMany()
            .HasForeignKey(o => o.PharmacyVisitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Sale)
            .WithOne(s => s.Order)
            .HasForeignKey<Sale>(s => s.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.Property(l => l.UnitPrice).HasColumnType("decimal(18,2)");
        builder.Property(l => l.DiscountPercent).HasColumnType("decimal(5,2)");
        builder.Property(l => l.CreatedByUserId).HasMaxLength(450);
        builder.Property(l => l.ModifiedByUserId).HasMaxLength(450);

        builder.HasOne(l => l.Order)
            .WithMany(o => o.Lines)
            .HasForeignKey(l => l.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Product)
            .WithMany()
            .HasForeignKey(l => l.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.Property(s => s.TotalAmount).HasColumnType("decimal(18,2)");
        builder.Property(s => s.CreatedByUserId).HasMaxLength(450);
        builder.Property(s => s.ModifiedByUserId).HasMaxLength(450);
        builder.HasIndex(s => s.OrderId).IsUnique();
        builder.HasIndex(s => s.SaleDateUtc);

        builder.HasOne(s => s.Pharmacy)
            .WithMany()
            .HasForeignKey(s => s.PharmacyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Representative)
            .WithMany()
            .HasForeignKey(s => s.RepresentativeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
