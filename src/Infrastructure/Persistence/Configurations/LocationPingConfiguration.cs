using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Infrastructure.Persistence.Configurations;

public class LocationPingConfiguration : IEntityTypeConfiguration<LocationPing>
{
    public void Configure(EntityTypeBuilder<LocationPing> builder)
    {
        builder.HasIndex(p => new { p.RepresentativeId, p.TimestampUtc });

        builder.HasOne(p => p.Representative)
            .WithMany()
            .HasForeignKey(p => p.RepresentativeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
