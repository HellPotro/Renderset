namespace Renderset.Core.Presets;

//public sealed class InMemoryReportPresetRepository
//    : IReportPresetRepository
//{
//    private readonly Dictionary<string, ReportPreset> _presets =
//        new(StringComparer.OrdinalIgnoreCase);

//    public ReportPreset? GetById(string id)
//    {
//        _presets.TryGetValue(id, out var preset);

//        return preset;
//    }

//    public IReadOnlyCollection<ReportPreset> GetAll()
//    {
//        return _presets.Values.ToList();
//    }

//    public void Save(ReportPreset preset)
//    {
//        ArgumentNullException.ThrowIfNull(preset);

//        if (string.IsNullOrWhiteSpace(preset.Id))
//            throw new ArgumentException(
//                "El preset debe tener un Id.",
//                nameof(preset));

//        _presets[preset.Id] = preset;
//    }
//}