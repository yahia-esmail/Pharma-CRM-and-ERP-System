using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Infrastructure.Persistence.Configurations;

public class FinancialReconciliationConfiguration : IEntityTypeConfiguration<FinancialReconciliation>
{
    public void Configure(EntityTypeBuilder<FinancialReconciliation> builder)
    {
        builder.Property(f => f.SystemBalance).HasColumnType("decimal(18,2)");
        builder.Property(f => f.CountedBalance).HasColumnType("decimal(18,2)");
        builder.Property(f => f.Reason).HasMaxLength(500);
        builder.Property(f => f.RejectionReason).HasMaxLength(500);
        builder.Property(f => f.RequestedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(f => f.ApprovedByUserId).HasMaxLength(450);
        builder.Property(f => f.CreatedByUserId).HasMaxLength(450);
        builder.Property(f => f.ModifiedByUserId).HasMaxLength(450);
        builder.HasIndex(f => new { f.RepresentativeId, f.Status });

        builder.HasOne(f => f.Representative)
            .WithMany()
            .HasForeignKey(f => f.RepresentativeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
