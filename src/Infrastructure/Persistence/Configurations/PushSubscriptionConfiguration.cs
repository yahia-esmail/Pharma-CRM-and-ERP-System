using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Infrastructure.Persistence.Configurations;

public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.Property(s => s.UserId).HasMaxLength(450).IsRequired();
        builder.Property(s => s.Endpoint).HasMaxLength(2048).IsRequired();
        builder.Property(s => s.EndpointHash).HasMaxLength(44).IsRequired(); // SHA-256, Base64
        builder.Property(s => s.P256dh).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Auth).HasMaxLength(100).IsRequired();
        builder.HasIndex(s => s.EndpointHash).IsUnique();
        builder.HasIndex(s => s.UserId);
    }
}
