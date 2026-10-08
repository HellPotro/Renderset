using Microsoft.EntityFrameworkCore;
using Renderset.Infrastructure.Persistence.Deca;

namespace Renderset.Infrastructure.Persistence;

/// <summary>
/// Base de datos Deca (ConnectionStrings:Deca), aislada de la de RenderSet.
/// Nada aquí apunta a tablas de RenderSet: el tenant es su id, y el PDF de
/// cada versión se guarda con ella.
/// </summary>
public sealed class DecaDbContext : DbContext
{
    /// <summary>
    /// Namespace de las entidades y configuraciones de este contexto.
    /// RenderSetDbContext lo excluye al cargar las suyas del ensamblado.
    /// </summary>
    public const string EntityNamespace = "Renderset.Infrastructure.Persistence.Deca";

    public DecaDbContext(
        DbContextOptions<DecaDbContext> options)
        : base(options)
    {
    }

    public DbSet<DecaDocumentEntity> DecaDocuments =>
        Set<DecaDocumentEntity>();

    public DbSet<DecaVersionEntity> DecaVersions =>
        Set<DecaVersionEntity>();

    public DbSet<DecaEventEntity> DecaEvents =>
        Set<DecaEventEntity>();

    public DbSet<DecaSequenceEntity> DecaSequences =>
        Set<DecaSequenceEntity>();

    public DbSet<DecaTemplateEntity> DecaTemplates =>
        Set<DecaTemplateEntity>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(DecaDbContext).Assembly,
            type => type.Namespace == EntityNamespace);
    }
}
