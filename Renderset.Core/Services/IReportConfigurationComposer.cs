using Renderset.Core.Blocks;
using Renderset.Core.Configurations;

namespace Renderset.Core.Services;

public interface IReportConfigurationComposer
{
    ReportConfiguration Compose(
        ReportConfiguration configuration,
        ReportBlock? headerBlock = null,
        ReportBlock? footerBlock = null);
}
