using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Infrastructure.Persistence.Configurations;

public class CollectionConfiguration : IEntityTypeConfiguration<Collection>
{
    public void Configure(EntityTypeBuilder<Collection> builder)
    {
        builder.Property(c => c.Amount).HasColumnType("decimal(18,2)");
        builder.Property(c => c.ReferenceNumber).HasMaxLength(100);
        builder.Property(c => c.Notes).HasMaxLength(500);
        builder.Property(c => c.CreatedByUserId).HasMaxLength(450);
        builder.Property(c => c.ModifiedByUserId).HasMaxLength(450);
        builder.HasIndex(c => c.CollectionDateUtc);
        builder.HasIndex(c => new { c.RepresentativeId, c.CollectionDateUtc });

        builder.HasOne(c => c.Representative)
            .WithMany()
            .HasForeignKey(c => c.RepresentativeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Pharmacy)
            .WithMany()
            .HasForeignKey(c => c.PharmacyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Sale)
            .WithMany()
            .HasForeignKey(c => c.SaleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CollectionAttachmentConfiguration : IEntityTypeConfiguration<CollectionAttachment>
{
    public void Configure(EntityTypeBuilder<CollectionAttachment> builder)
    {
        builder.Property(a => a.FileName).HasMaxLength(260).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.RelativePath).HasMaxLength(400).IsRequired();
        builder.Property(a => a.UploadedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(a => a.CreatedByUserId).HasMaxLength(450);
        builder.Property(a => a.ModifiedByUserId).HasMaxLength(450);

        builder.HasOne(a => a.Collection)
            .WithMany()
            .HasForeignKey(a => a.CollectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RemittanceTransactionConfiguration : IEntityTypeConfiguration<RemittanceTransaction>
{
    public void Configure(EntityTypeBuilder<RemittanceTransaction> builder)
    {
        builder.Property(r => r.Amount).HasColumnType("decimal(18,2)");
        builder.Property(r => r.ReceivingUserId).HasMaxLength(450).IsRequired();
        builder.Property(r => r.ReferenceNumber).HasMaxLength(100);
        builder.Property(r => r.CreatedByUserId).HasMaxLength(450);
        builder.Property(r => r.ModifiedByUserId).HasMaxLength(450);
        builder.HasIndex(r => new { r.RepresentativeId, r.RemittanceDateUtc });

        builder.HasOne(r => r.Representative)
            .WithMany()
            .HasForeignKey(r => r.RepresentativeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
