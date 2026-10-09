using EMS.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EMS.Infrastructure.Persistence.Configurations;

public class NumberSequenceConfiguration : IEntityTypeConfiguration<NumberSequence>
{
    public void Configure(EntityTypeBuilder<NumberSequence> builder)
    {
        builder.HasKey(n => n.NumberSequenceId);
        builder.Property(n => n.RowVersion).IsRowVersion();
        builder.HasIndex(n => new { n.Module, n.Year }).IsUnique();
    }
}
