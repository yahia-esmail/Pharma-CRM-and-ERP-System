using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Infrastructure.Persistence.Configurations;

public class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.Property(r => r.UserId).HasMaxLength(450);
        builder.Property(r => r.Key).HasMaxLength(100);
        builder.Property(r => r.Method).HasMaxLength(10);
        builder.Property(r => r.Path).HasMaxLength(500);
        builder.Property(r => r.RequestHash).HasMaxLength(44); // SHA-256 digest, Base64-encoded
        builder.Property(r => r.ResponseContentType).HasMaxLength(200);
        builder.Property(r => r.ResponseLocation).HasMaxLength(1000);

        // Keys are client-generated GUIDs, unique per user — also the guard against two concurrent
        // deliveries of the same request both getting through.
        builder.HasIndex(r => new { r.UserId, r.Key }).IsUnique();
        builder.HasIndex(r => r.CreatedAtUtc);
    }
}
