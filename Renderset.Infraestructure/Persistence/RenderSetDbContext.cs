using Microsoft.EntityFrameworkCore;
using Renderset.Infrastructure.Persistence.Entities;

namespace Renderset.Infrastructure.Persistence;

public sealed class RenderSetDbContext : DbContext
{
    public RenderSetDbContext(
        DbContextOptions<RenderSetDbContext> options)
        : base(options)
    {
    }

    public DbSet<ReportBlockEntity> ReportBlocks =>
        Set<ReportBlockEntity>();

    public DbSet<ReportEntity> Reports =>
        Set<ReportEntity>();

    public DbSet<ReportPresetEntity> ReportPresets =>
        Set<ReportPresetEntity>();

    public DbSet<ReportPresetAssignmentEntity> ReportPresetAssignments =>
        Set<ReportPresetAssignmentEntity>();

    public DbSet<ReportVariableEntity> ReportVariables =>
        Set<ReportVariableEntity>();

    public DbSet<TenantEntity> Tenants =>
        Set<TenantEntity>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(RenderSetDbContext).Assembly);
    }
}