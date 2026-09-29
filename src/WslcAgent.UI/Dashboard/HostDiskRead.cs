using WslcAgent.ApiClient;
using WslcAgent.ApiClient.Contracts;
using WslcAgent.UI.Components;

namespace WslcAgent.UI.Dashboard;

/// <summary>
/// The drive the sessions' VHDX files are on, read for the host's disk
/// reading and dial, and handed to the alarms' readings as well. Asked
/// whatever the session: the drive is Windows'.
/// </summary>
public sealed class HostDiskRead(WslcAgentApi api, AlarmReadings alarms)
    : SharedRead<HomeDisk>(TimeSpan.FromSeconds(5))
{
    protected override async Task<HomeDisk?> ReadAsync(CancellationToken cancellationToken)
    {
        var disk = await api.GetHomeDiskAsync(cancellationToken);
        alarms.Report(disk);
        return disk;
    }
}
