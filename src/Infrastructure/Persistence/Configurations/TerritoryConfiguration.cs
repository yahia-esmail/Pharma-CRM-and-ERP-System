using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Infrastructure.Persistence.Configurations;

public class TerritoryConfiguration : IEntityTypeConfiguration<Territory>
{
    public void Configure(EntityTypeBuilder<Territory> builder)
    {
        builder.Property(t => t.Name).HasMaxLength(150).IsRequired();
        builder.Property(t => t.Region).HasMaxLength(150);
        builder.Property(t => t.CreatedByUserId).HasMaxLength(450);
        builder.Property(t => t.ModifiedByUserId).HasMaxLength(450);
        builder.HasIndex(t => t.Name);

        // Territory <-> Representative is a circular relationship (a territory has representatives,
        // one of whom may be its district manager) — Restrict on both sides avoids a SQL Server
        // multiple-cascade-path error; deactivation is handled at the Application layer instead.
        builder.HasOne(t => t.DistrictManager)
            .WithMany()
            .HasForeignKey(t => t.DistrictManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Self-referencing hierarchy (addendum 3.2: Governorate -> City -> District -> Area). Restrict
        // avoids SQL Server's multiple-cascade-path error on a self-join.
        builder.HasOne(t => t.ParentTerritory)
            .WithMany(t => t.ChildTerritories)
            .HasForeignKey(t => t.ParentTerritoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
