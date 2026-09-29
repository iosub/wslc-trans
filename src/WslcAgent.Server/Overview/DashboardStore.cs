using Microsoft.Extensions.Options;
using WslcAgent.Server.Wslc;

namespace WslcAgent.Server.Overview;

/// <summary>
/// The dashboard arrangement kept with the user rather than on one device
/// (docs/home/spec.md, section 7), in <c>dashboard.json</c> in the agent's
/// data folder: the phone and the desktop then open the same one. The agent
/// stores it as the client wrote it and reads nothing inside it — the cards,
/// their cells and their parts are the client's business, and a client of a
/// later build must not lose what it does not yet understand.
///
/// A user who has none yet is given the default the agent ships with
/// (<c>dashboard.default.json</c>, embedded), written to their file the first
/// time it is asked for (the owner, 22 September 2026). While the dashboard is
/// being designed, a development build also writes that default back to the
/// repository, so the next installer ships what was designed; a release build
/// does not know where the repository is and writes no default (ShippedFile).
/// </summary>
public sealed class DashboardStore
{
    private readonly ShippedFile _default = new("dashboard.default.json", "DashboardDefaultSource");
    private readonly string _path;
    private readonly Lock _gate = new();
    private string? _arrangement;

    public DashboardStore(IOptions<WslcOptions> options)
    {
        _path = Path.Combine(options.Value.DataDirectory, "dashboard.json");
        _arrangement = ShippedFile.ReadFile(_path);
    }

    /// <summary>Whether this agent writes the default back to where it is built from (a development build).</summary>
    public bool WritesDefault => _default.Writable;

    /// <summary>What the user has; the default, kept as theirs, the first time they have none; null when there is no default either.</summary>
    public string? Get()
    {
        lock (_gate)
        {
            if (_arrangement is null && _default.Read() is { } fallback)
            {
                ShippedFile.WriteFile(_path, fallback);
                _arrangement = fallback;
            }

            return _arrangement;
        }
    }

    /// <summary>What the client wrote, kept as it was written; an empty body forgets it.</summary>
    public void Set(string? arrangement)
    {
        lock (_gate)
        {
            _arrangement = string.IsNullOrWhiteSpace(arrangement) ? null : arrangement;
            if (_arrangement is null)
            {
                File.Delete(_path);
            }
            else
            {
                ShippedFile.WriteFile(_path, _arrangement);
            }
        }
    }

    /// <summary>The default written back to the repository; false when this build does not write one (a release build).</summary>
    public bool SetDefault(string arrangement) => _default.Write(arrangement);
}
