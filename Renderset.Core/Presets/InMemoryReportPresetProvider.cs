namespace Renderset.Core.Presets;

//public sealed class InMemoryReportPresetProvider
//    : IReportPresetProvider
//{
//    private readonly IReportPresetRepository _presetRepository;
//    private readonly IReportPresetAssignmentRepository _assignmentRepository;

//    public InMemoryReportPresetProvider(
//        IReportPresetRepository presetRepository,
//        IReportPresetAssignmentRepository assignmentRepository)
//    {
//        _presetRepository = presetRepository;
//        _assignmentRepository = assignmentRepository;
//    }

//    public ReportPreset? GetPreset(
//        string reportId,
//        string? customerCode = null)
//    {
//        ReportPresetAssignment? assignment = null;

//        if (!string.IsNullOrWhiteSpace(customerCode))
//        {
//            assignment =
//                _assignmentRepository.GetForCustomer(
//                    reportId,
//                    customerCode);
//        }

//        assignment ??=
//            _assignmentRepository.GetDefault(
//                reportId);

//        if (assignment is null)
//            return null;

//        return _presetRepository.GetById(
//            assignment.PresetId);
//    }
//}