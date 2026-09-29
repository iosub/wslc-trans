using System.Net.Http;
using WslcAgent.ApiClient;
using WslcAgent.ApiClient.Contracts;

namespace WslcAgent.UI.Components;

/// <summary>One of the user's resources as the + list shows it: its uid in the agent's registry, its name, and a word about it.</summary>
public sealed record ResourceRow(int Uid, string Name, string Note);

/// <summary>
/// The user's resources a dashboard's resource cards show (docs/home/spec.md,
/// section 3), one family at a time: each read the way its list page reads
/// it, every row carrying its uid in the agent's registry, so a card finds
/// its resource through a rename or a recreate. The + list reads a family
/// through it when the family is chosen; Home, on its interval, every family
/// the dashboard has a card of.
/// </summary>
public sealed class DashboardResources(WslcAgentApi api)
{
    private readonly Dictionary<string, string> _errors = [];

    public ContainerListResponse? Containers { get; private set; }

    public ImageListResponse? Images { get; private set; }

    public VolumeListResponse? Volumes { get; private set; }

    public NetworkListResponse? Networks { get; private set; }

    /// <summary>Why the family's last read failed, or null when it did not.</summary>
    public string? Error(string kind) => _errors.GetValueOrDefault(kind);

    /// <summary>The family's last read failed: its cards say Unavailable instead of Loading.</summary>
    public bool Failed(string kind) => _errors.ContainsKey(kind);

    /// <summary>The family has been read at least once.</summary>
    public bool IsRead(string kind) => kind switch
    {
        DashboardCatalogue.Container => Containers is not null,
        DashboardCatalogue.Image => Images is not null,
        DashboardCatalogue.Volume => Volumes is not null,
        DashboardCatalogue.Network => Networks is not null,
        _ => false,
    };

    public ContainerSummary? Container(int uid) => Containers?.Containers.FirstOrDefault(c => c.Uid == uid);

    public ImageSummary? Image(int uid) => Images?.Images.FirstOrDefault(i => i.Uid == uid);

    public VolumeSummary? Volume(int uid) => Volumes?.Volumes.FirstOrDefault(v => v.Uid == uid);

    public NetworkSummary? Network(int uid) => Networks?.Networks.FirstOrDefault(n => n.Uid == uid);

    /// <summary>The name of the resource a card points at, as it is called now; null for a standard card or one not found.</summary>
    public string? NameOf(DashboardCard card) =>
        card.Ref is not { } reference ? null : Rows(card.Card).FirstOrDefault(row => row.Uid == reference.Uid)?.Name;

    /// <summary>The family's resources the registry numbers, by name: what the + list offers.</summary>
    public IReadOnlyList<ResourceRow> Rows(string kind)
    {
        IEnumerable<ResourceRow> rows = kind switch
        {
            DashboardCatalogue.Container => Containers?.Containers.Select(c => new ResourceRow(c.Uid, c.Name, c.State)) ?? [],
            DashboardCatalogue.Image => Images?.Images.Select(i => new ResourceRow(i.Uid, i.Reference, i.Size)) ?? [],
            DashboardCatalogue.Volume => Volumes?.Volumes.Select(v => new ResourceRow(v.Uid, v.Name, v.Driver)) ?? [],
            DashboardCatalogue.Network => Networks?.Networks.Select(n => new ResourceRow(n.Uid, n.Name, n.Driver)) ?? [],
            _ => [],
        };
        return [.. rows.Where(row => row.Uid > 0).OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// The family read again. Returns the uids it holds — a list read in full,
    /// so a card whose uid is missing points at a resource that was removed —
    /// or null when it could not be read, which removes nothing.
    /// </summary>
    public async Task<IReadOnlySet<int>?> RefreshAsync(string kind, CancellationToken cancellationToken = default)
    {
        try
        {
            switch (kind)
            {
                case DashboardCatalogue.Container:
                    Containers = await api.GetContainersAsync(cancellationToken: cancellationToken);
                    break;
                case DashboardCatalogue.Image:
                    Images = await api.GetImagesAsync(cancellationToken);
                    break;
                case DashboardCatalogue.Volume:
                    Volumes = await api.GetVolumesAsync(cancellationToken);
                    break;
                case DashboardCatalogue.Network:
                    Networks = await api.GetNetworksAsync(cancellationToken);
                    break;
                default:
                    return null;
            }
        }
        catch (Exception ex) when ((ex is AgentApiException or HttpRequestException) && !ex.IsSignInRequired())
        {
            _errors[kind] = ex.Message;
            return null;
        }
        catch (Exception ex) when (ex.IsSignInRequired())
        {
            // The sign-in screen takes over.
            return null;
        }

        _errors.Remove(kind);
        return Rows(kind).Select(row => row.Uid).ToHashSet();
    }
}
