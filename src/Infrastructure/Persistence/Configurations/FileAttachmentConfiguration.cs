using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Infrastructure.Persistence.Configurations;

public class FileAttachmentConfiguration : IEntityTypeConfiguration<FileAttachment>
{
    public void Configure(EntityTypeBuilder<FileAttachment> builder)
    {
        builder.Property(a => a.CreatedByUserId).HasMaxLength(450);
        builder.Property(a => a.ModifiedByUserId).HasMaxLength(450);
        builder.Property(a => a.UploadedByUserId).HasMaxLength(450);
        builder.Property(a => a.EntityType).HasMaxLength(100);

        builder.HasIndex(a => new { a.EntityType, a.EntityId });
    }
}
