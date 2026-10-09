using EMS.Domain.Workflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EMS.Infrastructure.Persistence.Configurations;

public class ApprovalInstanceConfiguration : IEntityTypeConfiguration<ApprovalInstance>
{
    public void Configure(EntityTypeBuilder<ApprovalInstance> builder)
    {
        builder.Property(a => a.RowVersion).IsRowVersion();
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
        builder.HasMany(a => a.Steps).WithOne(s => s.ApprovalInstance).HasForeignKey(s => s.ApprovalInstanceId);
    }
}

public class ApprovalStepConfiguration : IEntityTypeConfiguration<ApprovalStep>
{
    public void Configure(EntityTypeBuilder<ApprovalStep> builder)
    {
        builder.Property(s => s.RowVersion).IsRowVersion();
        builder.HasIndex(s => new { s.ApprovalInstanceId, s.Sequence }).IsUnique();
    }
}

public class ApprovalMatrixRuleConfiguration : IEntityTypeConfiguration<ApprovalMatrixRule>
{
    public void Configure(EntityTypeBuilder<ApprovalMatrixRule> builder)
    {
        builder.Property(r => r.RowVersion).IsRowVersion();
        builder.HasIndex(r => new { r.Module, r.Sequence, r.EffectiveFrom });
    }
}
