using WslcAgent.ApiClient.Contracts;
using WslcAgent.Mcp;
using WslcAgent.Server.Overview;
using WslcAgent.Server.Wslc;

namespace WslcAgent.Server.Endpoints;

/// <summary><c>/api/v1/home</c>: the Home page's overview cards and its three metrics, and <c>/api/v1/me/dashboard</c>, the arrangement kept with the user. See docs/api-v1.md.</summary>
public static class HomeEndpoints
{
    public static RouteGroupBuilder MapHomeEndpoints(this RouteGroupBuilder api)
    {
        // The dashboard as the user keeps it, for every client of this agent
        // (docs/home/spec.md, section 7): the client's own text, stored and
        // handed back as it was written. Empty means the user has none here
        // and the client offers to put its own there.
        api.MapGet("/me/dashboard", (DashboardStore store) =>
                Results.Text(store.Get() ?? "", "application/json; charset=utf-8"))
            .WithName("GetUserDashboard");

        // Every client is told, so a Home showing it reads it again when it is
        // written instead of asking every few seconds whether it was.
        api.MapPut("/me/dashboard", async (HttpRequest request, DashboardStore store, WslcEvents events) =>
            {
                store.Set(await ReadBodyAsync(request));
                events.Publish(new ChangeNotice([ChangeNotice.Dashboard]));
                return Results.NoContent();
            })
            .WithName("SetUserDashboard");

        // Home v2's dashboard (docs/home/v2/specv2.md, decision 20): a place
        // of its own beside today's, kept and handed back the same way. No
        // notice is published: a v2 dashboard is read when its page opens.
        api.MapGet("/me/dashboard-v2", (DashboardV2Store store) =>
                Results.Text(store.Get() ?? "", "application/json; charset=utf-8"))
            .WithName("GetUserDashboardV2");

        api.MapPut("/me/dashboard-v2", async (HttpRequest request, DashboardV2Store store) =>
            {
                store.Set(await ReadBodyAsync(request));
                return Results.NoContent();
            })
            .WithName("SetUserDashboardV2");

        // Home v2's default, the dashboard a user with none is given, written
        // back to the repository from the server's dashboard on a development
        // agent (the owner, 26 September 2026); a release build answers 409.
        // The default as the agent ships it, read by every client: a page or a
        // view left blank, on the agent or on a device, shows the default's
        // (the owner, 26 September 2026). Empty when none is shipped.
        api.MapGet("/dashboard-v2/default", (DashboardV2Store store) =>
                Results.Text(store.Default() ?? "", "application/json; charset=utf-8"))
            .WithName("GetDefaultDashboardV2");

        api.MapPut("/dashboard-v2/default", async (HttpRequest request, DashboardV2Store store) =>
                store.SetDefault(await ReadBodyAsync(request))
                    ? Results.NoContent()
                    : Results.Problem("Only a development build of the agent writes the default dashboard.", statusCode: StatusCodes.Status409Conflict))
            .WithName("SetDefaultDashboardV2");

        // The default a user with none is given, written back to the
        // repository while the dashboard is being designed (the owner,
        // 22 September 2026). Only a development build writes it; a release
        // build answers 409, which the client, calling it beside Copy to
        // server, lets pass.
        api.MapPut("/dashboard/default", async (HttpRequest request, DashboardStore store) =>
                store.SetDefault(await ReadBodyAsync(request))
                    ? Results.NoContent()
                    : Results.Problem("Only a development build of the agent writes the default dashboard.", statusCode: StatusCodes.Status409Conflict))
            .WithName("SetDefaultDashboard");

        // How each kind of card ships (the owner, 24 September 2026): read by
        // every client, and written back to the repository by the card
        // Settings' Save as default on a development agent; a release build,
        // which ships them, answers 409.
        api.MapGet("/dashboard/card-defaults", (CardDefaultsStore store) => new CardDefaultsResponse(store.Writable, store.Get()))
            .WithName("GetCardDefaults");

        // One card at a time, merged under its view and kind: two clients open
        // at once each sent their whole table, and the last to save wrote over
        // the other's.
        api.MapPut("/dashboard/card-defaults/{view}/{kind}", async (string view, string kind, HttpRequest request, CardDefaultsStore store) =>
                view is not ("desktop" or "mobile") || !IsCardKind(kind)
                    ? Results.Problem("A view is desktop or mobile, and a kind is a card's key.", statusCode: StatusCodes.Status400BadRequest)
                    : store.Set(view, kind, await ReadBodyAsync(request))
                        ? Results.NoContent()
                        : Results.Problem("Only a development build of the agent writes the cards' defaults.", statusCode: StatusCodes.Status409Conflict))
            .WithName("SetCardDefault");

        // How each kind of Home v2's object is born, view by view (the owner,
        // 26 September 2026; v2.5's landscape and portrait views beside v2's
        // desktop and mobile ones): read by every client, and written back to the
        // repository from the board of every object on a development agent,
        // one kind at a time; a release build, which ships them, answers 409.
        api.MapGet("/dashboard/object-defaults", (ObjectDefaultsStore store) => new ObjectDefaultsResponse(store.Writable, store.Get()))
            .WithName("GetObjectDefaults");

        api.MapPut("/dashboard/object-defaults/{view}/{type}", async (string view, string type, HttpRequest request, ObjectDefaultsStore store) =>
                view is not ("desktop" or "mobile" or "landscape" or "portrait") || !IsObjectType(type)
                    ? Results.Problem("A view is desktop, mobile, landscape or portrait, and a type is an object's (wslc.container-cpu).", statusCode: StatusCodes.Status400BadRequest)
                    : store.Set(view, type, await ReadBodyAsync(request))
                        ? Results.NoContent()
                        : Results.Problem("Only a development build of the agent writes the objects' defaults.", statusCode: StatusCodes.Status409Conflict))
            .WithName("SetObjectDefault");

        var group = api.MapGroup("/home");

        group.MapGet("", (IHomeService home, CancellationToken ct) => home.OverviewAsync(ct))
            .WithName("HomeOverview");

        group.MapGet("/metrics/runtime", (IHomeService home, CancellationToken ct) => home.RuntimeAsync(ct))
            .WithName("HomeRuntime");

        group.MapGet("/metrics/io", (IHomeService home, CancellationToken ct) => home.IoAsync(ct))
            .WithName("HomeIo");

        group.MapGet("/metrics/storage", (IHomeService home, CancellationToken ct) => home.StorageAsync(ct))
            .WithName("HomeStorage");

        group.MapGet("/metrics/disk", (IHomeService home) => home.Disk())
            .WithName("HomeDisk");

        return api;
    }

    /// <summary>A card kind's key: lower-case letters, digits and hyphens (cpu-chart, storage-vhdx).</summary>
    private static bool IsCardKind(string kind) =>
        kind.Length is > 0 and <= 64 && kind.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-');

    /// <summary>An object's type: lower-case letters, digits, hyphens and the dot after its owner (wslc.container-cpu).</summary>
    private static bool IsObjectType(string type) =>
        type.Length is > 0 and <= 96 && type.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '.');

    /// <summary>A dashboard body is the client's own text, taken as it came.</summary>
    private static async Task<string> ReadBodyAsync(HttpRequest request)
    {
        using var reader = new StreamReader(request.Body);
        return await reader.ReadToEndAsync();
    }
}
