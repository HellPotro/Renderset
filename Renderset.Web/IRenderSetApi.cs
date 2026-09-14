using Refit;
using Renderset.Core.Blocks;
using Renderset.Core.Persistence;
using Renderset.Core.Presets;
using Renderset.Core.Reports;
using Renderset.Core.Variables;

namespace Renderset.Web;

public interface IRenderSetApi
{
    #region Assignments

    [Get("/api/assignments/{tenantId}/{reportId}")]
    Task<IReadOnlyCollection<ReportPresetAssignment>> GetAssignmentsAsync(
        string tenantId,
        string reportId,
        CancellationToken cancellationToken = default);

    [Get("/api/assignments/{tenantId}/{reportId}/default")]
    Task<ReportPresetAssignment?> GetDefaultAssignmentAsync(
        string tenantId,
        string reportId,
        CancellationToken cancellationToken = default);

    [Get("/api/assignments/{tenantId}/{reportId}/{contextType}/{contextKey}")]
    Task<ReportPresetAssignment?> GetAssignmentAsync(
        string tenantId,
        string reportId,
        string contextType,
        string contextKey,
        CancellationToken cancellationToken = default);

    [Put("/api/assignments/{tenantId}")]
    Task SaveAssignmentAsync(
        string tenantId,
        [Body] ReportPresetAssignment assignment,
        CancellationToken cancellationToken = default);

    #endregion

    #region Blocks

    [Get("/api/blocks/{tenantId}")]
    Task<IReadOnlyCollection<ReportBlock>> GetBlocksAsync(
    string tenantId,
    CancellationToken cancellationToken = default);

    [Get("/api/blocks/{tenantId}/type/{type}")]
    Task<IReadOnlyCollection<ReportBlock>> GetBlocksByTypeAsync(
        string tenantId,
        ReportBlockType type,
        CancellationToken cancellationToken = default);

    [Get("/api/blocks/{tenantId}/{blockId}")]
    Task<ReportBlock> GetBlockAsync(
        string tenantId,
        string blockId,
        CancellationToken cancellationToken = default);

    [Get("/api/blocks/{tenantId}/{blockId}/resolved")]
    Task<ReportBlock> GetResolvedBlockAsync(
        string tenantId,
        string blockId,
        CancellationToken cancellationToken = default);

    [Put("/api/blocks/{tenantId}/{blockId}")]
    Task SaveBlockAsync(
        string tenantId,
        string blockId,
        [Body] ReportBlock block,
        CancellationToken cancellationToken = default);

    #endregion

    #region Presets

    [Get("/api/presets/{tenantId}/{presetId}")]
    Task<ReportPreset?> GetPresetAsync(
        string tenantId,
        string presetId,
        CancellationToken cancellationToken = default);

    [Get("/api/presets/{tenantId}")]
    Task<IReadOnlyCollection<ReportPreset>> GetPresetsAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    [Get("/api/presets/resolve/{tenantId}/{reportId}")]
    Task<ReportPreset?> ResolvePresetAsync(
        string tenantId,
        string reportId,
        [Query] string? contextType = null,
        [Query] string? contextKey = null,
        CancellationToken cancellationToken = default);

    [Put("/api/presets/{tenantId}/{presetId}")]
    Task SavePresetAsync(
        string tenantId,
        string presetId,
        [Body] ReportPreset preset,
        CancellationToken cancellationToken = default);

    #endregion

    #region Reports

    [Get("/api/reports/{tenantId}")]
    Task<IReadOnlyCollection<Report>> GetReportsAsync(
    string tenantId,
    CancellationToken cancellationToken = default);

    [Get("/api/reports/{tenantId}/{reportId}")]
    Task<Report> GetReportAsync(
        string tenantId,
        string reportId,
        CancellationToken cancellationToken = default);

    [Put("/api/reports/{tenantId}/{reportId}")]
    Task<IApiResponse> SaveReportAsync(
        string tenantId,
        string reportId,
        [Body] Report report,
        CancellationToken cancellationToken = default);

    #endregion

    #region Variables

    [Get("/api/variables/{tenantId}")]
    Task<IReadOnlyCollection<ReportVariable>> GetVariablesAsync(
    string tenantId,
    CancellationToken cancellationToken = default);

    [Put("/api/variables/{tenantId}/{key}")]
    Task SaveVariableAsync(
        string tenantId,
        string key,
        [Body] ReportVariable variable,
        CancellationToken cancellationToken = default);

    [Delete("/api/variables/{tenantId}/{key}")]
    Task DeleteVariableAsync(
        string tenantId,
        string key,
        CancellationToken cancellationToken = default);

    #endregion
}