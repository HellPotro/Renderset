namespace Renderset.Core.Themes;

public interface IReportThemeRepository
{
    Task<IReadOnlyCollection<TenantReportTheme>> GetAllAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    Task<TenantReportTheme?> GetByIdAsync(
        string tenantId,
        string themeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea o actualiza. Si viene marcado por defecto, desmarca el resto.
    /// </summary>
    Task SaveAsync(
        string tenantId,
        TenantReportTheme theme,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Baja lógica. Los presets que lo usaban siguen con su copia.
    /// </summary>
    Task<bool> DeleteAsync(
        string tenantId,
        string themeId,
        CancellationToken cancellationToken = default);
}
