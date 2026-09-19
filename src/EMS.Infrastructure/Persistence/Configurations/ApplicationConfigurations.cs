using EMS.Domain.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EMS.Infrastructure.Persistence.Configurations;

public class ShareholderApplicationConfiguration : IEntityTypeConfiguration<ShareholderApplication>
{
    public void Configure(EntityTypeBuilder<ShareholderApplication> builder)
    {
        builder.HasIndex(a => a.ApplicationNo).IsUnique();
        builder.Property(a => a.ApplicationNo).HasMaxLength(20).IsRequired();
        builder.Property(a => a.RowVersion).IsRowVersion();

        builder.HasOne(a => a.KycCase)
            .WithOne(k => k.ShareholderApplication!)
            .HasForeignKey<KycCase>(k => k.ShareholderApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.JointHolders).WithOne(j => j.ShareholderApplication).HasForeignKey(j => j.ShareholderApplicationId);
    }
}
