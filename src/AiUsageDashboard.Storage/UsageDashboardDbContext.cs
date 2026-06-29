using Microsoft.EntityFrameworkCore;

namespace AiUsageDashboard.Storage;

public sealed class UsageDashboardDbContext(DbContextOptions<UsageDashboardDbContext> options) : DbContext(options)
{
    public DbSet<UsageSnapshot> UsageSnapshots => Set<UsageSnapshot>();
    public DbSet<UsageMetricEntity> UsageMetrics => Set<UsageMetricEntity>();
    public DbSet<ApprovedModelEntity> ApprovedModels => Set<ApprovedModelEntity>();
    public DbSet<ModelMeterPriceEntity> ModelMeterPrices => Set<ModelMeterPriceEntity>();
    public DbSet<ModelQuotaEntity> ModelQuotas => Set<ModelQuotaEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UsageSnapshot>(entity =>
        {
            entity.HasIndex(x => new { x.Provider, x.Region, x.ModelId, x.WindowStart, x.WindowEnd });
            entity.Property(x => x.Provider).HasMaxLength(100);
            entity.Property(x => x.Region).HasMaxLength(100);
            entity.Property(x => x.ModelId).HasMaxLength(200);
            entity.Property(x => x.ModelAlias).HasMaxLength(200);
            entity.Property(x => x.EstimatedCostUsd).HasPrecision(18, 6);
            entity.HasMany(x => x.Metrics).WithOne().HasForeignKey(x => x.UsageSnapshotId).OnDelete(DeleteBehavior.Cascade);
            entity.Ignore(x => x.InputTokens);
            entity.Ignore(x => x.OutputTokens);
            entity.Ignore(x => x.CachedInputTokens);
            entity.Ignore(x => x.Requests);
        });

        modelBuilder.Entity<UsageMetricEntity>(entity =>
        {
            entity.HasIndex(x => new { x.UsageSnapshotId, x.Kind });
            entity.Property(x => x.Kind).HasConversion<string>().HasMaxLength(50);
            entity.Property(x => x.Quantity).HasPrecision(18, 6);
            entity.Property(x => x.Unit).HasMaxLength(50);
            entity.Property(x => x.Name).HasMaxLength(200);
        });

        modelBuilder.Entity<ApprovedModelEntity>(entity =>
        {
            entity.HasIndex(x => new { x.Provider, x.Region, x.ModelId }).IsUnique();
            entity.Property(x => x.Provider).HasMaxLength(100);
            entity.Property(x => x.Region).HasMaxLength(100);
            entity.Property(x => x.ModelId).HasMaxLength(200);
            entity.Property(x => x.Alias).HasMaxLength(200);
            entity.Property(x => x.EnvironmentTag).HasMaxLength(100);
        });

        modelBuilder.Entity<ModelMeterPriceEntity>(entity =>
        {
            entity.HasIndex(x => new { x.Provider, x.ModelId, x.MeterKind }).IsUnique();
            entity.Property(x => x.Provider).HasMaxLength(100);
            entity.Property(x => x.ModelId).HasMaxLength(200);
            entity.Property(x => x.MeterKind).HasConversion<string>().HasMaxLength(50);
            entity.Property(x => x.PriceUsd).HasPrecision(18, 6);
            entity.Property(x => x.UnitQuantity).HasPrecision(18, 6);
            entity.Property(x => x.Unit).HasMaxLength(50);
        });

        modelBuilder.Entity<ModelQuotaEntity>(entity =>
        {
            entity.HasIndex(x => new { x.Provider, x.Region, x.ModelId, x.MeterKind, x.QuotaName }).IsUnique();
            entity.Property(x => x.Provider).HasMaxLength(100);
            entity.Property(x => x.Region).HasMaxLength(100);
            entity.Property(x => x.ModelId).HasMaxLength(200);
            entity.Property(x => x.MeterKind).HasConversion<string>().HasMaxLength(50);
            entity.Property(x => x.Limit).HasPrecision(18, 6);
            entity.Property(x => x.QuotaName).HasMaxLength(300);
            entity.Property(x => x.Unit).HasMaxLength(50);
        });
    }
}
