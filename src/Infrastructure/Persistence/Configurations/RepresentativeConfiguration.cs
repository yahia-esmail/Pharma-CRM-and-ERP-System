using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Infrastructure.Persistence.Configurations;

public class RepresentativeConfiguration : IEntityTypeConfiguration<Representative>
{
    public void Configure(EntityTypeBuilder<Representative> builder)
    {
        builder.Property(r => r.EmployeeCode).HasMaxLength(50).IsRequired();
        builder.Property(r => r.FullName).HasMaxLength(200).IsRequired();
        builder.Property(r => r.CreatedByUserId).HasMaxLength(450);
        builder.Property(r => r.ModifiedByUserId).HasMaxLength(450);
        builder.HasIndex(r => r.EmployeeCode).IsUnique();

        builder.HasOne(r => r.Territory)
            .WithMany(t => t.Representatives)
            .HasForeignKey(r => r.TerritoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ReportingManager)
            .WithMany(r => r.DirectReports)
            .HasForeignKey(r => r.ReportingManagerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
