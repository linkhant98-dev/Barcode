using EMS.Domain.CorporateActions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EMS.Infrastructure.Persistence.Configurations;

public class BonusEventConfiguration : IEntityTypeConfiguration<BonusEvent>
{
    public void Configure(EntityTypeBuilder<BonusEvent> builder)
    {
        builder.HasIndex(b => b.BonusEventNo).IsUnique();
        builder.Property(b => b.RowVersion).IsRowVersion();
        builder.HasMany(b => b.Entitlements).WithOne(e => e.BonusEvent).HasForeignKey(e => e.BonusEventId);
    }
}

public class BonusEntitlementConfiguration : IEntityTypeConfiguration<BonusEntitlement>
{
    public void Configure(EntityTypeBuilder<BonusEntitlement> builder)
    {
        builder.HasKey(e => e.BonusEntitlementId);
        builder.Property(e => e.EligibleShares).HasPrecision(19, 6);
        builder.Property(e => e.RawBonusEntitlement).HasPrecision(19, 6);
        builder.Property(e => e.BonusShares).HasPrecision(19, 6);
        builder.Property(e => e.RemainderShares).HasPrecision(19, 6);
        builder.Property(e => e.NewTotalShares).HasPrecision(19, 6);
        builder.HasOne(e => e.Shareholder).WithMany().HasForeignKey(e => e.ShareholderId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DividendEventConfiguration : IEntityTypeConfiguration<DividendEvent>
{
    public void Configure(EntityTypeBuilder<DividendEvent> builder)
    {
        builder.HasIndex(d => d.DividendEventNo).IsUnique();
        builder.Property(d => d.RowVersion).IsRowVersion();
        builder.HasMany(d => d.Entitlements).WithOne(e => e.DividendEvent).HasForeignKey(e => e.DividendEventId);
    }
}

public class DividendEntitlementConfiguration : IEntityTypeConfiguration<DividendEntitlement>
{
    public void Configure(EntityTypeBuilder<DividendEntitlement> builder)
    {
        builder.Property(e => e.OldShares).HasPrecision(19, 6);
        builder.Property(e => e.NewShares).HasPrecision(19, 6);
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasOne(e => e.Shareholder).WithMany().HasForeignKey(e => e.ShareholderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Settlements).WithOne(s => s.DividendEntitlement).HasForeignKey(s => s.DividendEntitlementId);
    }
}
