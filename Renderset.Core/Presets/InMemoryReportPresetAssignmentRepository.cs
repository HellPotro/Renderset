namespace Renderset.Core.Presets;

//public sealed class InMemoryReportPresetAssignmentRepository
//    : IReportPresetAssignmentRepository
//{
//    private readonly List<ReportPresetAssignment> _assignments = [];

//    public ReportPresetAssignment? GetForCustomer(
//        string reportId,
//        string customerCode)
//    {
//        return _assignments.FirstOrDefault(x =>
//            string.Equals(
//                x.ReportId,
//                reportId,
//                StringComparison.OrdinalIgnoreCase)
//            &&
//            string.Equals(
//                x.CustomerCode,
//                customerCode,
//                StringComparison.OrdinalIgnoreCase));
//    }

//    public ReportPresetAssignment? GetDefault(
//        string reportId)
//    {
//        return _assignments.FirstOrDefault(x =>
//            string.Equals(
//                x.ReportId,
//                reportId,
//                StringComparison.OrdinalIgnoreCase)
//            &&
//            x.CustomerCode is null);
//    }

//    public void Save(
//        ReportPresetAssignment assignment)
//    {
//        ArgumentNullException.ThrowIfNull(assignment);

//        var existing =
//            _assignments.FirstOrDefault(x =>
//                string.Equals(
//                    x.ReportId,
//                    assignment.ReportId,
//                    StringComparison.OrdinalIgnoreCase)
//                &&
//                string.Equals(
//                    x.CustomerCode,
//                    assignment.CustomerCode,
//                    StringComparison.OrdinalIgnoreCase));

//        if (existing is not null)
//        {
//            _assignments.Remove(existing);
//        }

//        _assignments.Add(
//            assignment);
//    }
//}