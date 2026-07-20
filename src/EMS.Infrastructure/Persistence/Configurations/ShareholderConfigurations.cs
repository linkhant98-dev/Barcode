using EMS.Domain.Shareholders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EMS.Infrastructure.Persistence.Configurations;

public class ShareholderConfiguration : IEntityTypeConfiguration<Shareholder>
{
    public void Configure(EntityTypeBuilder<Shareholder> builder)
    {
        builder.HasIndex(s => s.ShareholderNo).IsUnique();
        builder.Property(s => s.ShareholderNo).HasMaxLength(20).IsRequired();
        builder.Property(s => s.RowVersion).IsRowVersion();

        builder.HasOne(s => s.Person)
            .WithOne(p => p.Shareholder!)
            .HasForeignKey<Person>(p => p.ShareholderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Corporate)
            .WithOne(c => c.Shareholder!)
            .HasForeignKey<Corporate>(c => c.ShareholderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.JointHolders).WithOne(j => j.Shareholder).HasForeignKey(j => j.ShareholderId);
        builder.HasMany(s => s.Addresses).WithOne(a => a.Shareholder).HasForeignKey(a => a.ShareholderId);
        builder.HasMany(s => s.Contacts).WithOne(c => c.Shareholder).HasForeignKey(c => c.ShareholderId);
        builder.HasMany(s => s.BankAccounts).WithOne(b => b.Shareholder).HasForeignKey(b => b.ShareholderId);
        builder.HasMany(s => s.RelationshipDeclarations).WithOne(r => r.Shareholder).HasForeignKey(r => r.ShareholderId);

        builder.HasOne(s => s.ShareholderGroup).WithMany().HasForeignKey(s => s.ShareholderGroupId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class RelationshipDeclarationConfiguration : IEntityTypeConfiguration<RelationshipDeclaration>
{
    public void Configure(EntityTypeBuilder<RelationshipDeclaration> builder)
    {
        builder.HasKey(r => r.DeclarationId);
    }
}

public class CorporateConfiguration : IEntityTypeConfiguration<Corporate>
{
    public void Configure(EntityTypeBuilder<Corporate> builder)
    {
        builder.HasKey(c => c.CorporateId);
        builder.HasMany(c => c.Signatories).WithOne(s => s.Corporate).HasForeignKey(s => s.CorporateId);
        builder.HasMany(c => c.BeneficialOwners).WithOne(b => b.Corporate).HasForeignKey(b => b.CorporateId);
    }
}
