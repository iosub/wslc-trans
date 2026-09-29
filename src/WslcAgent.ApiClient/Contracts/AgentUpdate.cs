namespace WslcAgent.ApiClient.Contracts;

/// <summary>
/// Body of <c>PUT /api/v1/agent/update/settings</c>: whether the agent installs
/// a newer version of itself as soon as one is in its package folder.
/// </summary>
/// <param name="AutoUpdate">On, the agent updates itself when a newer installer appears and nothing is being transferred; off, only Update now does.</param>
public sealed record AgentUpdateSettings(bool AutoUpdate);

/// <summary>
/// Body of <c>GET /api/v1/agent/update</c>: Settings → Agent update, the
/// switch and what it would do right now.
/// </summary>
/// <param name="Settings">What the operator has chosen.</param>
/// <param name="Version">The version running.</param>
/// <param name="Installed">
/// Whether this agent runs from the folder the installer put it in. Only that
/// one updates itself: a development build is not replaced by an installer.
/// </param>
/// <param name="AvailableVersion">The version of the agent installer in the package folder; empty when there is none.</param>
/// <param name="Newer">Whether that installer is newer than the version running.</param>
/// <param name="InFlight">File transfers and backups under way, which an update waits for, since it would cut them.</param>
/// <param name="State"><see cref="AgentUpdateState"/>.</param>
/// <param name="SecondsLeft">While <see cref="AgentUpdateState.Announced"/>, the seconds before it starts; a client counts down from its own clock, not the agent's.</param>
/// <param name="LastResult">How the last update ended, in words, or empty when there has been none.</param>
public sealed record AgentUpdateStatus(
    AgentUpdateSettings Settings,
    string Version,
    bool Installed,
    string AvailableVersion,
    bool Newer,
    int InFlight,
    string State,
    int SecondsLeft,
    string LastResult);

/// <summary>Where an update stands.</summary>
public static class AgentUpdateState
{
    /// <summary>Nothing to do, or nothing asked.</summary>
    public const string Idle = "idle";

    /// <summary>Asked for, and waiting for the transfers under way to end.</summary>
    public const string Waiting = "waiting";

    /// <summary>
    /// An automatic update told to every client, which counts down and offers
    /// Cancel: anyone can stop it before it starts.
    /// </summary>
    public const string Announced = "announced";

    /// <summary>The installer has been handed over; the agent is about to stop and come back as the new version.</summary>
    public const string Installing = "installing";
}
