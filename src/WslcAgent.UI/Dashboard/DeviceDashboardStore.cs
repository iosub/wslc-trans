using Berpiztu.Dashboard.Storage;
using Microsoft.JSInterop;

namespace WslcAgent.UI.Dashboard;

/// <summary>
/// Home v2.5's dashboard kept on this device (<c>wslcAgent.dashboardV25</c>),
/// both its views as one text, as today's Home keeps its own there: a place
/// of its own beside it, neither reading the other. A private window keeps
/// nothing, and the dashboard still works for as long as the page is open.
/// </summary>
public sealed class DeviceDashboardStore(IJSRuntime js) : IDashboardStore
{
    public async Task<string?> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return TransfersCardPieces.Upgrade(await js.InvokeAsync<string?>("wslcAgent.dashboardV25", cancellationToken));
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or TaskCanceledException)
        {
            return null;
        }
    }

    public async Task SaveAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            await js.InvokeAsync<string?>("wslcAgent.dashboardV25", cancellationToken, text);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or TaskCanceledException)
        {
            // Private windows: the dashboard is shown, it is just not remembered.
        }
    }
}
