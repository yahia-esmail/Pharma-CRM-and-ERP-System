using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Infrastructure.Persistence.Configurations;

public class PerformanceTargetConfiguration : IEntityTypeConfiguration<PerformanceTarget>
{
    public void Configure(EntityTypeBuilder<PerformanceTarget> builder)
    {
        builder.Property(t => t.MetricKey).HasMaxLength(100).IsRequired();
        builder.Property(t => t.TargetValue).HasColumnType("decimal(18,2)");
        builder.Property(t => t.CreatedByUserId).HasMaxLength(450);
        builder.Property(t => t.ModifiedByUserId).HasMaxLength(450);

        builder.HasIndex(t => new { t.RepresentativeId, t.Year, t.Month, t.MetricKey }).IsUnique();

        builder.HasOne(t => t.Representative)
            .WithMany()
            .HasForeignKey(t => t.RepresentativeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
