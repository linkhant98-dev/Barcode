using EMS.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EMS.Infrastructure.Persistence.Configurations;

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.HasKey(r => r.RolePermissionId);
        builder.Property(r => r.RoleName).HasMaxLength(64).IsRequired();
        builder.Property(r => r.PermissionKey).HasMaxLength(64).IsRequired();
        builder.HasIndex(r => new { r.RoleName, r.PermissionKey }).IsUnique();
    }
}
