using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.Property(a => a.EntityType).HasMaxLength(200).IsRequired();
        // Composite Identity keys (e.g. AspNetUserRoles: UserId,RoleId) can exceed a single GUID's length.
        builder.Property(a => a.EntityId).HasMaxLength(300).IsRequired();
        builder.Property(a => a.PerformedByUserId).HasMaxLength(450);
        builder.Property(a => a.IpAddress).HasMaxLength(64);
        builder.Property(a => a.OldValuesJson).HasColumnType("nvarchar(max)");
        builder.Property(a => a.NewValuesJson).HasColumnType("nvarchar(max)");

        builder.HasIndex(a => new { a.EntityType, a.EntityId });
        builder.HasIndex(a => a.TimestampUtc);
    }
}
