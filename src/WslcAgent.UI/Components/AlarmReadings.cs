using System.Net.Http;
using WslcAgent.ApiClient;
using WslcAgent.ApiClient.Contracts;

namespace WslcAgent.UI.Components;

/// <summary>
/// The readings the dashboard's alarms watch (the owner, 22 September 2026):
/// the host's CPU and memory, its drive, and the containers' own. Home samples
/// them for its cards and reports them here; the bottom bar, on any screen,
/// asks for what its alarms need and is not fresh, so the two never read the
/// same thing twice on the same beat. What a measure is — and whether a card's
/// alarm is up — is worked out here alone, for the cards and the bar alike.
/// </summary>
public sealed class AlarmReadings(WslcAgentApi api, SessionState session)
{
    /// <summary>A reading younger than this is the one to use; Home's beat is five seconds.</summary>
    private static readonly TimeSpan Fresh = TimeSpan.FromSeconds(4);

    private DateTimeOffset _runtimeAt;
    private DateTimeOffset _diskAt;
    private DateTimeOffset _containersAt;

    /// <summary>The host's last CPU and memory sample.</summary>
    public HomeRuntime? Runtime { get; private set; }

    /// <summary>The drive the sessions' VHDX files are on, as last read.</summary>
    public HomeDisk? Disk { get; private set; }

    /// <summary>The containers as last listed, with their stats.</summary>
    public IReadOnlyList<ContainerSummary>? Containers { get; private set; }

    /// <summary>A reading arrived.</summary>
    public event Action? Changed;

    /// <summary>What is used of the drive, in percent; null until it is read or when it could not be.</summary>
    public double? DiskPercent => HostUsage.DiskPercent(Disk);

    public void Report(HomeRuntime runtime)
    {
        if (runtime.Error)
        {
            return;
        }

        Runtime = runtime;
        _runtimeAt = DateTimeOffset.UtcNow;
        Changed?.Invoke();
    }

    public void Report(HomeDisk disk)
    {
        Disk = disk;
        _diskAt = DateTimeOffset.UtcNow;
        Changed?.Invoke();
    }

    public void Report(IReadOnlyList<ContainerSummary> containers)
    {
        Containers = containers;
        _containersAt = DateTimeOffset.UtcNow;
        Changed?.Invoke();
    }

    /// <summary>
    /// The measure a card's alarm watches, in percent: on a container's card
    /// the container's own, while it runs; on any other the host's — the CPU
    /// used of all there is, the memory used of the session's, the drive's.
    /// Null while it is not known, which raises no alarm.
    /// </summary>
    public double? MeasureOf(DashboardCard card, string measure)
    {
        if (card.Ref is { } reference)
        {
            return Containers?.FirstOrDefault(c => c.Uid == reference.Uid) is { IsRunning: true } container
                ? Percents.Parse(measure == DashboardCatalogue.CpuMeasure ? container.CpuPercent : container.MemPercent)
                : null;
        }

        return measure switch
        {
            DashboardCatalogue.CpuMeasure => HostUsage.CpuPercent(Runtime),
            DashboardCatalogue.MemoryMeasure => HostUsage.MemoryPercent(Runtime),
            DashboardCatalogue.DiskMeasure => DiskPercent,
            _ => null,
        };
    }

    /// <summary>One alarm of a card is up: its measure is past its threshold.</summary>
    public bool IsUp(DashboardCard card, CardAlarm alarm) => MeasureOf(card, alarm.Measure) is { } value && value > alarm.Threshold;

    /// <summary>The card's alarm is up: any of the measures it watches is past its threshold.</summary>
    public bool InAlarm(DashboardCard card) => DashboardCatalogue.AlarmsOf(card).Any(alarm => IsUp(card, alarm));

    /// <summary>
    /// Reads what these alarms need and nobody has reported lately: the host's
    /// CPU and memory and the containers while the session runs (a stopped one
    /// holds nothing, and asking would open it again), the drive always. A read
    /// that fails keeps the last one.
    /// </summary>
    public async Task SampleAsync(IReadOnlyCollection<(DashboardCard Card, string Measure)> alarms, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        try
        {
            if (session.Active && now - _runtimeAt > Fresh && alarms.Any(a => a.Card.Ref is null && a.Measure != DashboardCatalogue.DiskMeasure))
            {
                Report(await api.GetHomeRuntimeAsync(cancellationToken));
            }

            if (now - _diskAt > Fresh && alarms.Any(a => a.Measure == DashboardCatalogue.DiskMeasure))
            {
                Report(await api.GetHomeDiskAsync(cancellationToken));
            }

            if (session.Active && now - _containersAt > Fresh && alarms.Any(a => a.Card.Ref is not null))
            {
                Report((await api.GetContainersAsync(cancellationToken: cancellationToken)).Containers);
            }
        }
        catch (Exception ex) when (ex is AgentApiException or HttpRequestException)
        {
            // The last readings stand; the sign-in screen, when that is the cause, takes over.
        }
    }
}
