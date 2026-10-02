using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Infrastructure.Persistence.Configurations;

public class LocationPingConfiguration : IEntityTypeConfiguration<LocationPing>
{
    public void Configure(EntityTypeBuilder<LocationPing> builder)
    {
        builder.HasIndex(p => new { p.RepresentativeId, p.TimestampUtc });

        builder.Property(p => p.Source).HasMaxLength(16).HasDefaultValue(LocationPingSources.Track);

        // De-duplicates re-sent batches. Filtered: pings from older app versions have no client id.
        builder.HasIndex(p => new { p.RepresentativeId, p.ClientId })
            .IsUnique()
            .HasFilter("[ClientId] IS NOT NULL");

        builder.HasOne(p => p.Representative)
            .WithMany()
            .HasForeignKey(p => p.RepresentativeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
