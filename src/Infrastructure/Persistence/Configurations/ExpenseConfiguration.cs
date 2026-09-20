using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaERP.Domain.Entities;

namespace PharmaERP.Infrastructure.Persistence.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.Property(e => e.CreatedByUserId).HasMaxLength(450);
        builder.Property(e => e.ModifiedByUserId).HasMaxLength(450);
        builder.Property(e => e.ApprovedByUserId).HasMaxLength(450);
        builder.Property(e => e.ReimbursedByUserId).HasMaxLength(450);

        builder.HasIndex(e => new { e.CreatedByUserId, e.Status });

        builder.HasOne(e => e.Territory)
            .WithMany()
            .HasForeignKey(e => e.TerritoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
