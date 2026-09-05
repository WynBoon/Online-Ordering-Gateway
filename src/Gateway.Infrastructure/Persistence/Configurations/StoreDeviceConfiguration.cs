using Gateway.Domain.Devices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gateway.Infrastructure.Persistence.Configurations;

public sealed class StoreDeviceConfiguration : IEntityTypeConfiguration<StoreDevice>
{
    public void Configure(EntityTypeBuilder<StoreDevice> builder)
    {
        builder.ToTable("StoreDevices");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Name).HasMaxLength(200);
        builder.Property(d => d.EnrollmentCodeHash).HasMaxLength(64);
        builder.Property(d => d.TokenHash).HasMaxLength(64);
        builder.Property(d => d.HardwareFingerprint).HasMaxLength(200);
        builder.Property(d => d.Functions).HasConversion<int>();
        builder.Property(d => d.Status).HasConversion<int>();
        builder.HasIndex(d => d.StoreId);
        builder.HasIndex(d => d.TokenHash);
        builder.HasIndex(d => d.EnrollmentCodeHash);
    }
}
