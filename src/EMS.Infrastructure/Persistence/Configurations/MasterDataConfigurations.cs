using EMS.Domain.Common;
using EMS.Domain.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EMS.Infrastructure.Persistence.Configurations;

internal static class MasterDataConfigurationHelper
{
    /// <summary>16.2 - filtered unique index on Code for active rows only, so a deactivated code can be reused.</summary>
    public static void ConfigureMasterData<T>(EntityTypeBuilder<T> builder) where T : MasterDataEntity
    {
        builder.Property(m => m.Code).HasMaxLength(30).IsRequired();
        builder.Property(m => m.RowVersion).IsRowVersion();
        builder.HasIndex(m => m.Code).IsUnique().HasFilter("[IsActive] = 1");
    }
}

public class NrcPrefixConfiguration : IEntityTypeConfiguration<NrcPrefix>
{
    public void Configure(EntityTypeBuilder<NrcPrefix> builder) => MasterDataConfigurationHelper.ConfigureMasterData(builder);
}

public class GeographyConfiguration : IEntityTypeConfiguration<Geography>
{
    public void Configure(EntityTypeBuilder<Geography> builder) => MasterDataConfigurationHelper.ConfigureMasterData(builder);
}

public class BankBranchConfiguration : IEntityTypeConfiguration<BankBranch>
{
    public void Configure(EntityTypeBuilder<BankBranch> builder) => MasterDataConfigurationHelper.ConfigureMasterData(builder);
}

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder) => MasterDataConfigurationHelper.ConfigureMasterData(builder);
}

public class ShareholderGroupConfiguration : IEntityTypeConfiguration<ShareholderGroup>
{
    public void Configure(EntityTypeBuilder<ShareholderGroup> builder) => MasterDataConfigurationHelper.ConfigureMasterData(builder);
}

public class ShareClassConfiguration : IEntityTypeConfiguration<ShareClass>
{
    public void Configure(EntityTypeBuilder<ShareClass> builder) => MasterDataConfigurationHelper.ConfigureMasterData(builder);
}

public class DocumentTypeConfiguration : IEntityTypeConfiguration<DocumentType>
{
    public void Configure(EntityTypeBuilder<DocumentType> builder) => MasterDataConfigurationHelper.ConfigureMasterData(builder);
}

public class ReasonCodeConfiguration : IEntityTypeConfiguration<ReasonCode>
{
    public void Configure(EntityTypeBuilder<ReasonCode> builder) => MasterDataConfigurationHelper.ConfigureMasterData(builder);
}
