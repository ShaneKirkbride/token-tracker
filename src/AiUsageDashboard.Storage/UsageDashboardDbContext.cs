using Microsoft.EntityFrameworkCore;

namespace AiUsageDashboard.Storage;

public sealed class UsageDashboardDbContext(DbContextOptions<UsageDashboardDbContext> options) : DbContext(options)
{
    public DbSet<UsageSnapshot> UsageSnapshots => Set<UsageSnapshot>();
    public DbSet<ApprovedModelEntity> ApprovedModels => Set<ApprovedModelEntity>();
    public DbSet<ModelPriceEntity> ModelPrices => Set<ModelPriceEntity>();

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

        modelBuilder.Entity<ModelPriceEntity>(entity =>
        {
            entity.HasIndex(x => new { x.Provider, x.ModelId }).IsUnique();
            entity.Property(x => x.Provider).HasMaxLength(100);
            entity.Property(x => x.ModelId).HasMaxLength(200);
            entity.Property(x => x.InputPer1MTokensUsd).HasPrecision(18, 6);
            entity.Property(x => x.OutputPer1MTokensUsd).HasPrecision(18, 6);
            entity.Property(x => x.CachedInputPer1MTokensUsd).HasPrecision(18, 6);
        });
    }
}
