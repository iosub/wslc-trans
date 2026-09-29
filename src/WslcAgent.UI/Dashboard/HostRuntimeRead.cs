using WslcAgent.ApiClient;
using WslcAgent.ApiClient.Contracts;
using WslcAgent.UI.Components;

namespace WslcAgent.UI.Dashboard;

/// <summary>
/// The containers' aggregate CPU and memory, read for the host's readings,
/// dials and charts, and handed to the alarms' readings as well, so the
/// bottom bar does not read it again on the same beat. Not asked while the
/// session is stopped.
/// </summary>
public sealed class HostRuntimeRead(WslcAgentApi api, SessionState session, AlarmReadings alarms)
    : SampledRead<HomeRuntime>(TimeSpan.FromSeconds(5))
{
    protected override bool CanRead => session.Active;

    protected override async Task<HomeRuntime?> ReadAsync(CancellationToken cancellationToken)
    {
        var runtime = await api.GetHomeRuntimeAsync(cancellationToken);
        alarms.Report(runtime);
        return runtime;
    }

    protected override bool Failed(HomeRuntime sample) => sample.Error;
}
