using EMS.Domain.Shares;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EMS.Infrastructure.Persistence.Configurations;

public class ShareTransactionConfiguration : IEntityTypeConfiguration<ShareTransaction>
{
    public void Configure(EntityTypeBuilder<ShareTransaction> builder)
    {
        builder.HasIndex(t => t.TransactionNo).IsUnique();
        builder.Property(t => t.TransactionNo).HasMaxLength(20).IsRequired();
        builder.Property(t => t.RowVersion).IsRowVersion();
    }
}

// Shared-primary-key one-to-one "extension table" pattern: ShareIssue/ShareTransfer row IDs equal their
// owning ShareTransaction ID, so each transaction has at most one issue or transfer detail row.
public class ShareIssueConfiguration : IEntityTypeConfiguration<ShareIssue>
{
    public void Configure(EntityTypeBuilder<ShareIssue> builder)
    {
        builder.HasKey(i => i.TransactionId);
        builder.Property(i => i.NumberOfShares).HasPrecision(19, 6);
        builder.HasOne(i => i.Transaction).WithOne(t => t.ShareIssue!).HasForeignKey<ShareIssue>(i => i.TransactionId);
        builder.HasOne(i => i.Shareholder).WithMany().HasForeignKey(i => i.ShareholderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.ShareClass).WithMany().HasForeignKey(i => i.ShareClassId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ShareTransferConfiguration : IEntityTypeConfiguration<ShareTransfer>
{
    public void Configure(EntityTypeBuilder<ShareTransfer> builder)
    {
        builder.HasKey(t => t.TransactionId);
        builder.Property(t => t.Quantity).HasPrecision(19, 6);
        builder.HasOne(t => t.Transaction).WithOne(tx => tx.ShareTransfer!).HasForeignKey<ShareTransfer>(t => t.TransactionId);
        builder.HasOne(t => t.FromShareholder).WithMany().HasForeignKey(t => t.FromShareholderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.ToShareholder).WithMany().HasForeignKey(t => t.ToShareholderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.ShareClass).WithMany().HasForeignKey(t => t.ShareClassId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ShareLedgerEntryConfiguration : IEntityTypeConfiguration<ShareLedgerEntry>
{
    public void Configure(EntityTypeBuilder<ShareLedgerEntry> builder)
    {
        builder.HasKey(l => l.LedgerId);
        builder.Property(l => l.QuantityDelta).HasPrecision(19, 6);
        builder.Property(l => l.RunningQuantityBalance).HasPrecision(19, 6);
        builder.HasOne(l => l.Shareholder).WithMany().HasForeignKey(l => l.ShareholderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(l => l.ShareClass).WithMany().HasForeignKey(l => l.ShareClassId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.ShareholderId, l.ShareClassId, l.EffectiveDate });
    }
}

public class ShareCertificateConfiguration : IEntityTypeConfiguration<ShareCertificate>
{
    public void Configure(EntityTypeBuilder<ShareCertificate> builder)
    {
        builder.Property(c => c.Quantity).HasPrecision(19, 6);
        builder.Property(c => c.RowVersion).IsRowVersion();
        builder.HasIndex(c => c.CertificateNumber).IsUnique();
        builder.HasOne(c => c.Shareholder).WithMany().HasForeignKey(c => c.ShareholderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.ShareClass).WithMany().HasForeignKey(c => c.ShareClassId).OnDelete(DeleteBehavior.Restrict);
    }
}
