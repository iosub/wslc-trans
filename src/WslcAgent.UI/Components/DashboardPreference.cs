using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;
using WslcAgent.ApiClient;

namespace WslcAgent.UI.Components;

/// <summary>
/// A card on the dashboard: its own id, given when it was placed and never
/// changed; the kind it is (a <see cref="DashboardCatalogue"/> key); its size
/// in cells (<see cref="W"/> by <see cref="H"/>); the cell it stands in
/// (<see cref="X"/>, <see cref="Y"/>, minus one until it has been placed);
/// how its parts are laid out inside it, once the user has laid them out;
/// and, for a resource card, the reference the card resolves each refresh.
/// The id is the card's, not the resource's: a resource's WSLC id changes
/// when it is recreated and its name when it is renamed, and the card keeps
/// its place through both (docs/home/spec.md, section 3).
/// </summary>
/// <param name="Parts">Where each part stands inside the card, in the card's own cells, and at what type size; null until the user has touched them, when they stand at their defined sizes.</param>
/// <param name="Alarms">The card's alarms as the user set them in its Settings, one per measure; a measure not in it has its kind's own (<see cref="DashboardCatalogue.AlarmSetting"/>).</param>
public sealed record DashboardCard(
    string Id,
    string Card,
    int W = DashboardCatalogue.DefaultColumns,
    int H = DashboardCatalogue.DefaultRows,
    int X = -1,
    int Y = -1,
    IReadOnlyList<CardPart>? Parts = null,
    CardReference? Ref = null,
    IReadOnlyList<CardAlarm>? Alarms = null);

/// <summary>
/// One of a card's alarms (the owner, 22 September 2026): the measure it
/// watches — CPU, memory or disk, the whole host's on a system card, the
/// container's own on a container's, which watches both at once when both are
/// on — and the percent past which the card turns red and blinks. Off keeps
/// the threshold and says nothing.
/// </summary>
public sealed record CardAlarm(string Measure, int Threshold = CardAlarm.DefaultThreshold, bool Off = false)
{
    public const int DefaultThreshold = 90;
}

/// <summary>
/// One of the dashboard's alarms shown in the bottom bar as well (the owner,
/// 22 September 2026): the card that holds it, by its own id, and the measure.
/// Each view keeps its own, since a phone's bar holds fewer.
/// </summary>
public sealed record StatusAlarm(string Card, string Measure);

/// <summary>
/// A part's type size (the owner, 22 September 2026), each step one of the
/// theme's four (docs/RULES.md): Medium is the part as it always was, Small a
/// step down, Large a step up where the theme has one. Nothing adjusts
/// itself; the user chooses it in the part's Settings.
/// </summary>
public enum TypeSize
{
    Small,
    Medium,
    Large,
}

/// <summary>One part of a card (<see cref="CardKind.Parts"/>) in the card's cells: where it stands, what it spans, its type size, and whether the card shows it.</summary>
/// <param name="Hidden">Hidden in the card's Settings (the owner, 22 September 2026): not drawn, its cells free, its place kept for when it is shown again.</param>
public sealed record CardPart(string Key, int X, int Y, int W, int H, TypeSize Size = TypeSize.Medium, bool Hidden = false);

/// <summary>
/// What a resource card points at: the resource's uid in the agent's registry
/// (<c>resources.json</c>, the owner, 22 September 2026), the one place that
/// says which resource is which. No rename and no recreate changes it; its
/// name and WSLC id are always read from the registry, never kept here.
/// </summary>
public sealed record CardReference(int Uid);

/// <summary>
/// Which of the dashboard's two arrangements is on screen (the owner,
/// 22 September 2026): each is designed on its own, and the window's width
/// chooses between them — up to 600 px, the width the stylesheet's phone rules
/// start at, the mobile one — again whenever it changes, a phone turned
/// included.
/// </summary>
public enum DashboardView
{
    Desktop,
    Mobile,
}

/// <summary>Where the dashboard is kept, this device's choice (docs/home/spec.md, section 7).</summary>
public enum DashboardScope
{
    /// <summary>On this device (<c>wslcAgent.dashboard</c>), as the theme and the table-or-cards choice are.</summary>
    Device,

    /// <summary>With the user, on the agent (<c>/api/v1/me/dashboard</c>): the phone and the desktop open the same one.</summary>
    User,
}

/// <summary>
/// The dashboard's cards, where they stand and at what size, as the user left
/// them — on this device (<c>wslcAgent.dashboard</c>), the way <see cref="ViewPreference"/>
/// keeps the table-or-cards choice of each list, or with the user on the agent
/// (<c>/api/v1/me/dashboard</c>), whichever this device is set to
/// (<see cref="Scope"/>). Positions are cells of the grid (<see cref="DashboardLayout"/>),
/// kept whatever the window's width: the page says how many columns it shows
/// (<see cref="Fit"/>), and scrolls to the rest when the cards reach further
/// (<see cref="Width"/>). Inside a card,
/// its parts are laid out on the card's own cells by the same engine. A fresh
/// install shows every kind once, in the catalogue's order, at four cells by
/// four. There are two arrangements, the desktop's and the mobile's, each
/// designed on its own; the window's width says which is on screen
/// (<see cref="View"/>), and Edit edits that one. Every change is saved the
/// moment it is made, so leaving the dashboard never has anything to ask about.
/// </summary>
public sealed class DashboardPreference(IJSRuntime js, WslcAgentApi api)
{
    /// <summary>
    /// What is stored: the desktop arrangement's cards and the columns they
    /// were placed for, the version of the form, and the mobile arrangement,
    /// absent until the mobile view has been shown. Version 4 is the quarter
    /// cell (the owner, 22 September 2026: a cell half as wide and half as high
    /// as the day's half cell, the dashboard made again); what an earlier build
    /// stored is in other cells, so its sizes and places cannot be kept.
    /// </summary>
    private sealed record Stored(int Columns, List<DashboardCard> Cards, int Version = 0, StoredView? Mobile = null, List<StatusAlarm>? Status = null);

    /// <summary>The mobile arrangement as it is stored: its cards, the columns they were placed for, and the alarms its bar shows.</summary>
    private sealed record StoredView(int Columns, List<DashboardCard> Cards, List<StatusAlarm>? Status = null);

    /// <summary>One of the two arrangements: its cards, in reading order, the columns they were placed for, and the alarms the bottom bar shows with it.</summary>
    private sealed class Arrangement(int reference, List<DashboardCard> cards, List<StatusAlarm>? status = null)
    {
        public int Reference { get; set; } = reference;

        public List<DashboardCard> Cards { get; set; } = cards;

        public List<StatusAlarm> Status { get; set; } = status ?? [];
    }

    private const int FormVersion = 4;

    /// <summary>The form placed in half cells, read once more with every number doubled.</summary>
    private const int HalfCellVersion = 3;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>How one kind of card ships in one view: its size and how its parts stand in it.</summary>
    private sealed record CardDefault(int W, int H, List<CardPart> Parts);

    /// <summary>The same options, indented: the defaults are a file in the repository, read in a diff.</summary>
    private static readonly JsonSerializerOptions JsonIndented = new(Json) { WriteIndented = true };

    private Arrangement _desktop = new(0, Defaults());
    private Arrangement? _mobile;
    private Task? _loading;

    /// <summary>
    /// How each kind of card ships, by view ("desktop", "mobile") and kind
    /// (the agent's card-defaults.json, the owner, 24 September 2026): what a
    /// card nobody has laid out shows, what a card added opens at, what Reset
    /// layout brings back. Empty until the agent has said, and then the
    /// stacked layout of before stands in.
    /// </summary>
    private Dictionary<string, Dictionary<string, CardDefault>> _cardDefaults = [];

    /// <summary>This agent writes the cards' defaults back to its repository (a development build): the card Settings offer Save as default.</summary>
    public bool CanSaveDefaults { get; private set; }

    /// <summary>The stored text this device last read or wrote where the dashboard is kept: a read that finds the same has nothing new.</summary>
    private string? _known;

    /// <summary>
    /// Where this device reads and writes the dashboard. A device that never
    /// chose starts with the user's (the owner, 22 September 2026): its own
    /// storage holds nothing yet, and the user's is at least the default the
    /// agent ships with.
    /// </summary>
    public DashboardScope Scope { get; private set; } = DashboardScope.User;

    /// <summary>The arrangement on screen: the desktop's or the mobile's, as the window's width says.</summary>
    public DashboardView View { get; private set; }

    /// <summary>The cards on screen, in reading order.</summary>
    public IReadOnlyList<DashboardCard> Cards => Arranged.Cards;

    /// <summary>The columns the grid has now, as the page measured them; zero until it has.</summary>
    public int Columns { get; private set; }

    /// <summary>
    /// The arrangement the view shows and every edit changes. The mobile one,
    /// the first time it is shown, starts as the desktop's cards flowing in
    /// reading order (the owner, 22 September 2026), and is its own from then on.
    /// </summary>
    private Arrangement Arranged => View == DashboardView.Mobile ? (_mobile ??= Flowed(_desktop)) : _desktop;

    /// <summary>The cards placed for the grid as it is now.</summary>
    public IReadOnlyList<PlacedCard> Placed => DashboardLayout.Place(Arranged.Cards, Columns);

    /// <summary>The columns the dashboard has: those the window shows, or more when the cards reach further, which the page then scrolls to.</summary>
    public int Width => Math.Max(Columns, DashboardLayout.Extent(Placed));

    /// <summary>Whether any card of this kind is on the dashboard (the + list's tick, the sampling of what is on screen).</summary>
    public bool IsShown(string key) => Arranged.Cards.Any(card => card.Card == key);

    public DashboardCard? Find(string? id) => id is null ? null : Arranged.Cards.FirstOrDefault(card => card.Id == id);

    /// <summary>Raised when the set, a place, a size, a layout or a setting changed.</summary>
    public event Action? Changed;

    /// <summary>
    /// Reads what this device remembers, once. A host where the browser is not
    /// reachable yet (the native clients, whose web view loads after the first
    /// components) gets the default set and asks again next time.
    /// </summary>
    public Task ReadyAsync() => _loading ??= LoadAsync();

    /// <summary>
    /// The grid was measured: this many columns, in a window narrow enough for
    /// the mobile view or not. The page redraws; nothing is saved by a
    /// measurement. Returns whether the view changed, which lets the selection go.
    /// </summary>
    public bool Fit(int columns, bool mobile)
    {
        Columns = Math.Max(0, columns);
        var view = mobile ? DashboardView.Mobile : DashboardView.Desktop;
        if (view == View)
        {
            return false;
        }

        View = view;
        return true;
    }

    /// <summary>What a place holds, for Copy to ask before it writes over one that is there.</summary>
    public Task<string?> PeekAsync(DashboardScope scope) => ReadAsync(scope);

    /// <summary>
    /// The dashboard read again where it is kept, and shown when it changed
    /// (the owner, 22 September 2026): the phone and the desktop keep the same
    /// one with the user, and what one of them saved has to reach the other
    /// without restarting it — and before the other edits, or its stale copy
    /// of both views would be written over the one just saved. With
    /// <paramref name="always"/> it is shown even when the text is the one
    /// this device last knew: a change of view loads the view from where the
    /// dashboard is kept, never from what this device held of it.
    /// </summary>
    public async Task RefreshAsync(bool always = false)
    {
        var stored = await ReadAsync(Scope);
        if (stored is not { Length: > 0 } || (stored == _known && !always))
        {
            return;
        }

        _known = stored;
        (_desktop, _mobile) = Parse(stored);
        Changed?.Invoke();
    }

    /// <summary>
    /// Where this device keeps the dashboard from now on (docs/home/spec.md,
    /// section 7), remembered on the device. Switching shows what that place
    /// holds and writes nothing (the owner, 22 September 2026: switching never
    /// overwrites; Copy is the one verb that does). A place that holds nothing
    /// yet shows the fresh set, as a new device does, and keeps it only once
    /// it is edited there.
    /// </summary>
    public async Task UseAsync(DashboardScope scope)
    {
        if (scope == Scope)
        {
            return;
        }

        var there = await ReadAsync(scope);
        Scope = scope;
        try
        {
            await js.InvokeAsync<string?>("wslcAgent.dashboardScope", scope == DashboardScope.User ? User : Device);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or TaskCanceledException)
        {
            // Private windows: the choice holds for this run.
        }

        _known = there;
        (_desktop, _mobile) = there is { Length: > 0 } ? Parse(there) : (new Arrangement(0, Defaults()), null);
        Changed?.Invoke();
    }

    /// <summary>
    /// The arrangement on screen written to the other place, this one staying
    /// where it is (the owner, 22 September 2026): the desktop's dashboard
    /// given to the user without the desktop leaving its own, and the other
    /// way round. Given to the user, it is also written as the default the
    /// agent ships with, while the dashboard is being designed — a development
    /// agent keeps it, a release agent refuses it and nothing is lost. The
    /// default carries the system cards alone (the owner, 22 September 2026):
    /// every machine it is installed on has containers and images of its own,
    /// so a resource card made here would point at nothing there.
    /// </summary>
    public async Task CopyToAsync(DashboardScope scope)
    {
        await WriteAsync(scope, Text);
        if (scope != DashboardScope.User)
        {
            return;
        }

        try
        {
            await api.SetDefaultDashboardAsync(SystemText);
        }
        catch (Exception ex) when (ex is AgentApiException or HttpRequestException or TaskCanceledException)
        {
            // A release agent writes no default: the user's copy is what mattered.
        }
    }

    private const string Device = "device";

    private const string User = "user";

    /// <summary>
    /// The card dragged to a cell takes it only when the cells are free (the
    /// owner, 22 September 2026: nothing the hand does not hold ever moves;
    /// the user makes the room, and a card that does not fit stays where it
    /// was). Returns whether it moved.
    /// </summary>
    public async Task<bool> MoveAsync(string id, int x, int y)
    {
        var placed = Editable();
        var card = placed.FirstOrDefault(p => p.Card.Id == id);
        if (card is null)
        {
            return false;
        }

        // Never lower than just under the lowest edge on the dashboard, the
        // card's own included (the owner, 22 September 2026: a card carried off
        // by the pane's own scrolling landed at row 810, out of reach; bounded
        // by the others alone, the lowest card could not go down at all).
        var bottom = placed.Select(p => p.Y + p.H).DefaultIfEmpty(0).Max();
        var moved = card with
        {
            X = Math.Clamp(x, 0, Math.Max(0, Math.Max(Columns, DashboardLayout.Extent(placed)) - card.W)),
            Y = Math.Clamp(y, 0, bottom),
        };
        if (moved == card || !DashboardLayout.IsFree(placed, moved))
        {
            return false;
        }

        await ApplyAsync(placed.Select(p => p.Card.Id == id ? moved : p));
        return true;
    }

    /// <summary>The resources the dashboard shows, by their uid in the agent's registry, with the kind of card each has: what the + list opens with.</summary>
    public IReadOnlyDictionary<int, string> Resources =>
        Arranged.Cards.Where(card => card.Ref is { Uid: > 0 }).DistinctBy(card => card.Ref!.Uid).ToDictionary(card => card.Ref!.Uid, card => card.Card);

    /// <summary>
    /// The + list's Save (the owner, 22 September 2026: the list applies
    /// nothing until it is saved): the dashboard shows these standard kinds and
    /// these resources, and nothing else. A card that leaves leaves its cells
    /// empty; one that joins takes the first free cells, in the catalogue's
    /// order and then the resources', so its arrival moves nothing already
    /// placed. One save for the lot.
    /// </summary>
    public Task ShowOnlyAsync(IReadOnlySet<string> kinds, IReadOnlyDictionary<int, string> resources)
    {
        var changed = Arranged.Cards.RemoveAll(card => card.Ref is { } reference ? !resources.ContainsKey(reference.Uid) : !kinds.Contains(card.Card) && !IsFixed(card)) > 0;
        var placed = Editable();
        var joining = DashboardCatalogue.Standard.Where(kind => kinds.Contains(kind.Key) && !IsShown(kind.Key))
            .Select(kind => NewCard(kind, View))
            .Concat(resources.Where(resource => !Resources.ContainsKey(resource.Key))
                .Select(resource => (Uid: resource.Key, Kind: DashboardCatalogue.Find(resource.Value)))
                .Where(resource => resource.Kind is { IsResource: true })
                .Select(resource => NewCard(resource.Kind!, View) with { Ref = new CardReference(resource.Uid) }))
            .ToList();
        foreach (var card in joining)
        {
            placed.Add(DashboardLayout.FirstFit(placed, DashboardLayout.Span(card, Columns), Columns));
        }

        return changed || joining.Count > 0 ? ApplyAsync(placed) : Task.CompletedTask;
    }

    /// <summary>
    /// The card asked for a size in cells, from Settings or from its corner
    /// handle. It takes it only when the cells it would grow into are free,
    /// the same rule as a move; its parts, if laid out, are fitted to the new
    /// cells. Returns whether it changed.
    /// </summary>
    public async Task<bool> ResizeAsync(string id, int columns, int rows)
    {
        var card = Find(id);
        if (card is null)
        {
            return false;
        }

        var w = DashboardCatalogue.ClampColumns(columns, Math.Max(1, Width));
        var h = DashboardCatalogue.ClampRows(rows);
        if (w == card.W && h == card.H)
        {
            return false;
        }

        var placed = Editable();
        var current = placed.First(p => p.Card.Id == id);
        var width = Math.Max(Columns, DashboardLayout.Extent(placed));
        var resized = DashboardLayout.Span(current.Card with { W = w, H = h, Parts = Refit(current.Card.Parts, w, h) }, width);
        resized = resized with { X = Math.Clamp(current.X, 0, Math.Max(0, width - resized.W)), Y = current.Y };
        if (!DashboardLayout.IsFree(placed, resized))
        {
            return false;
        }

        await ApplyAsync(placed.Select(p => p.Card.Id == id ? resized : p));
        return true;
    }

    /// <summary>
    /// A card put back as it was — its size, its parts and its alarms — when
    /// a Settings that applied its changes as they were made is cancelled or
    /// closed without saving (the owner, 24 September 2026). The size only
    /// where its cells are free, as any resize; its parts with it, since they
    /// were laid out for it. Returns whether the size came back.
    /// </summary>
    public async Task<bool> RestoreAsync(DashboardCard snapshot)
    {
        if (Find(snapshot.Id) is not { } current)
        {
            return false;
        }

        var sized = (current.W, current.H) == (snapshot.W, snapshot.H) || await ResizeAsync(snapshot.Id, snapshot.W, snapshot.H);
        if (Find(snapshot.Id) is { } card)
        {
            await ReplaceAsync(card with { Parts = sized ? snapshot.Parts : card.Parts, Alarms = snapshot.Alarms });
        }

        return sized;
    }

    /// <summary>A part's type size, from its Settings; the card's parts are written down first if they were not yet.</summary>
    public Task SetPartSizeAsync(string id, string key, TypeSize size)
    {
        var card = Find(id);
        if (card is null)
        {
            return Task.CompletedTask;
        }

        var parts = PartsOf(card);
        if (parts.FirstOrDefault(p => p.Key == key) is not { } part || part.Size == size)
        {
            return Task.CompletedTask;
        }

        return ReplaceAsync(card with { Parts = [.. parts.Select(p => p.Key == key ? p with { Size = size } : p)] });
    }

    /// <summary>
    /// A part shown or hidden, from the card's Settings (the owner, 22 September
    /// 2026). Hidden, it is not drawn and its cells are free; shown again, it
    /// takes the place it had when that is still free, and the first free
    /// cells otherwise — nothing already there moves for it.
    /// </summary>
    public Task SetPartShownAsync(string id, string key, bool shown)
    {
        var card = Find(id);
        if (card is null)
        {
            return Task.CompletedTask;
        }

        var parts = PartsOf(card);
        if (parts.FirstOrDefault(p => p.Key == key) is not { } part || part.Hidden == !shown)
        {
            return Task.CompletedTask;
        }

        if (!shown)
        {
            return ReplaceAsync(card with { Parts = [.. parts.Select(p => p.Key == key ? p with { Hidden = true } : p)] });
        }

        var placed = parts.Where(p => !p.Hidden).Select(AsCard).ToList();
        var back = AsCard(part with { Hidden = false, X = Math.Clamp(part.X, 0, Math.Max(0, card.W - part.W)), Y = Math.Clamp(part.Y, 0, Math.Max(0, card.H - part.H)) });
        if (!DashboardLayout.IsFree(placed, back))
        {
            back = DashboardLayout.FirstFit(placed, back with { X = -1, Y = -1 }, card.W);
        }

        return ReplaceAsync(card with { Parts = AsParts([.. placed, back], [.. parts.Select(p => p.Key == key ? p with { Hidden = false } : p)]) });
    }

    /// <summary>
    /// The cards of one kind whose uid the registry no longer holds — the
    /// resource removed, and its uid with it (the owner, 22 September 2026) —
    /// leave the dashboard, in both arrangements, and the cells they stood in
    /// stay empty. Called only with a list the agent read in full, never with
    /// nothing because it could not.
    /// </summary>
    public Task DropRemovedAsync(string kind, IReadOnlySet<int> live)
    {
        bool Gone(DashboardCard card) => card.Card == kind && card.Ref is { } reference && !live.Contains(reference.Uid);
        var dropped = _desktop.Cards.RemoveAll(Gone) + (_mobile?.Cards.RemoveAll(Gone) ?? 0);
        return dropped == 0 ? Task.CompletedTask : ChangedAsync();
    }

    /// <summary>
    /// The alarms the bottom bar shows for the view on screen (the owner,
    /// 22 September 2026): those still on a card of it and still switched on,
    /// in the order they were ticked. An alarm switched off, or whose card has
    /// left, leaves the bar with it.
    /// </summary>
    public IReadOnlyList<StatusAlarm> Status =>
        [.. Arranged.Status.Where(status => Find(status.Card) is { } card && DashboardCatalogue.AlarmsOf(card).Any(alarm => alarm.Measure == status.Measure))];

    /// <summary>The bar's alarms for the view on screen, from its Status alarms list's Save.</summary>
    public Task SetStatusAsync(IEnumerable<StatusAlarm> status)
    {
        Arranged.Status = [.. status.Distinct()];
        return ChangedAsync();
    }

    /// <summary>One of the card's alarms, from its Settings, in the place of the one on the same measure; the card keeps them with its place and its parts.</summary>
    public Task SetAlarmAsync(string id, CardAlarm alarm) =>
        Find(id) is { } card
            ? ReplaceAsync(card with { Alarms = [.. (card.Alarms ?? []).Where(a => a.Measure != alarm.Measure), alarm] })
            : Task.CompletedTask;

    /// <summary>The one card taken off the dashboard (the − button); the hole it leaves stays. A card that is always there stays.</summary>
    public Task HideAsync(string id)
    {
        var card = Find(id);
        if (card is null || IsFixed(card))
        {
            return Task.CompletedTask;
        }

        Arranged.Cards.Remove(card);
        return ChangedAsync();
    }

    /// <summary>
    /// Where a card's parts stand: as the user laid them out; until then, as
    /// its kind ships in the view on screen (<see cref="SaveAsDefaultAsync"/>),
    /// with any part the kind gained since in the first free cells; and when
    /// the kind ships no layout, each part at its defined size in the order the
    /// kind names them (<see cref="StackedParts"/>).
    /// </summary>
    public IReadOnlyList<CardPart> PartsOf(DashboardCard card) =>
        card.Parts
        ?? (DefaultOf(card.Card, View) is { } shipped ? WithNewParts(card.Card, shipped.Parts, card.W) : StackedParts(card));

    /// <summary>The size a card of this kind ships at in the view on screen; null when it ships none, and the catalogue's stands.</summary>
    public (int W, int H)? DefaultSize(string kind) => DefaultOf(kind, View) is { } shipped ? (shipped.W, shipped.H) : null;

    /// <summary>
    /// The card as it stands now — its size, how its parts stand, their type
    /// sizes, which of them show — made how its kind ships in the view on
    /// screen (the owner, 24 September 2026), and written back by the
    /// development agent to its repository, so the next installer ships it.
    /// Every card of the kind nobody has laid out follows at once. False when
    /// the agent refused (a release build) or could not be reached.
    /// </summary>
    public async Task<bool> SaveAsDefaultAsync(string id)
    {
        if (Find(id) is not { } card)
        {
            return false;
        }

        // This card alone, merged by the agent under its view and kind: the
        // whole table sent from this client's copy wrote over what another
        // client open at the same time had saved (the owner's Images and
        // Volumes, mobile, 24 September 2026). Read back afterwards, so this
        // client holds the others' saves as well as its own.
        var entry = new CardDefault(card.W, card.H, [.. PartsOf(card)]);
        try
        {
            await api.SetCardDefaultAsync(ViewKey(View), card.Card, JsonSerializer.Serialize(entry, JsonIndented));
        }
        catch (Exception ex) when (ex is AgentApiException or HttpRequestException)
        {
            return false;
        }

        await ReadDefaultsAsync();
        Changed?.Invoke();
        return true;
    }

    /// <summary>How a kind ships in a view: its own, or the desktop's when the mobile view has none of its own.</summary>
    private CardDefault? DefaultOf(string kind, DashboardView view) =>
        _cardDefaults.GetValueOrDefault(ViewKey(view))?.GetValueOrDefault(kind)
        ?? (view == DashboardView.Mobile ? _cardDefaults.GetValueOrDefault(ViewKey(DashboardView.Desktop))?.GetValueOrDefault(kind) : null);

    private static string ViewKey(DashboardView view) => view == DashboardView.Mobile ? "mobile" : "desktop";

    /// <summary>What the agent ships of the cards' defaults, and whether it writes them back; nothing when it cannot be reached, and the stacked layout stands in.</summary>
    private async Task ReadDefaultsAsync()
    {
        try
        {
            var response = await api.GetCardDefaultsAsync();
            CanSaveDefaults = response.Writable;
            _cardDefaults = ParseDefaults(response.Defaults);
        }
        catch (Exception ex) when (ex is AgentApiException or HttpRequestException or TaskCanceledException)
        {
            // The layouts stay the stacked ones; the next load asks again.
        }
    }

    /// <summary>The defaults text read: kinds this build knows, parts of those kinds; anything else is left out, and text that is none of this is no defaults.</summary>
    private static Dictionary<string, Dictionary<string, CardDefault>> ParseDefaults(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, CardDefault>>>(text, Json) ?? [];
            return parsed.ToDictionary(
                view => view.Key,
                view => view.Value
                    .Where(kind => DashboardCatalogue.Find(kind.Key) is not null && kind.Value is { W: >= 1, H: >= 1, Parts: not null })
                    .ToDictionary(kind => kind.Key, kind => kind.Value with
                    {
                        Parts = [.. kind.Value.Parts.Where(part => DashboardCatalogue.Find(kind.Key)!.Parts.Contains(part.Key)).DistinctBy(part => part.Key)],
                    }));
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// The layout of a kind that ships none: each part at its defined size
    /// (<see cref="DashboardCatalogue.PartSize"/>) in the order the kind names
    /// them, from the top left, in the card's columns. A part the card has no
    /// room for stands below its last row, out of sight until the card is
    /// given the rows.
    /// </summary>
    private static IReadOnlyList<CardPart> StackedParts(DashboardCard card)
    {
        var placed = new List<PlacedCard>();
        foreach (var key in DashboardCatalogue.Find(card.Card)?.Parts ?? [])
        {
            var (w, h) = DashboardCatalogue.PartSize(card.Card, key);
            placed.Add(DashboardLayout.FirstFit(placed, AsCard(new CardPart(key, -1, -1, Math.Min(w, card.W), h)), card.W));
        }

        return AsParts(placed, []);
    }

    /// <summary>Layout begins on a card: the places its parts have, defined or laid out, are written down so the hand changes those.</summary>
    public Task StartLayoutAsync(string id)
    {
        var card = Find(id);
        return card is null || card.Parts is not null ? Task.CompletedTask : ReplaceAsync(card with { Parts = PartsOf(card) });
    }

    /// <summary>The card's parts back where they always stood: stacked, the card's own way.</summary>
    public Task ResetLayoutAsync(string id)
    {
        var card = Find(id);
        return card is null || card.Parts is null ? Task.CompletedTask : ReplaceAsync(card with { Parts = null });
    }

    /// <summary>A part dragged to a cell of its card takes it only when the cells are free, the same rule as a card on the dashboard. Returns whether it moved.</summary>
    public async Task<bool> MovePartAsync(string id, string key, int x, int y)
    {
        var card = Find(id);
        var placed = card?.Parts is null ? null : card.Parts.Where(p => !p.Hidden).Select(AsCard).ToList();
        var part = placed?.FirstOrDefault(p => p.Card.Id == key);
        if (card is null || placed is null || part is null)
        {
            return false;
        }

        var moved = part with { X = Math.Clamp(x, 0, Math.Max(0, card.W - part.W)), Y = Math.Clamp(y, 0, Math.Max(0, card.H - part.H)) };
        if (moved == part || !DashboardLayout.IsFree(placed, moved))
        {
            return false;
        }

        await ReplaceAsync(card with { Parts = AsParts(placed.Select(p => p.Card.Id == key ? moved : p), card.Parts) });
        return true;
    }

    /// <summary>A part asked for a size in its card's cells, from its handle or its settings: never beyond the card, and only into free cells. Returns whether it changed.</summary>
    public async Task<bool> ResizePartAsync(string id, string key, int columns, int rows)
    {
        var card = Find(id);
        var placed = card?.Parts is null ? null : card.Parts.Where(p => !p.Hidden).Select(AsCard).ToList();
        var part = placed?.FirstOrDefault(p => p.Card.Id == key);
        if (card is null || placed is null || part is null)
        {
            return false;
        }

        var w = Math.Clamp(columns, 1, card.W);
        var h = Math.Clamp(rows, 1, card.H);
        if (w == part.W && h == part.H)
        {
            return false;
        }

        var resized = part with { W = w, H = h, X = Math.Clamp(part.X, 0, card.W - w), Y = Math.Clamp(part.Y, 0, card.H - h) };
        if (!DashboardLayout.IsFree(placed, resized))
        {
            return false;
        }

        await ReplaceAsync(card with { Parts = AsParts(placed.Select(p => p.Card.Id == key ? resized : p), card.Parts) });
        return true;
    }

    /// <summary>The parts of a card that changed size, kept inside it: those still inside stay as they are; those the new edges cut through are cut to them and placed where they fit.</summary>
    private static IReadOnlyList<CardPart>? Refit(IReadOnlyList<CardPart>? parts, int w, int h)
    {
        if (parts is null)
        {
            return null;
        }

        // A part that still stands inside the card keeps its place and its size
        // (the owner, 22 September 2026: a resize never puts anything back to a
        // default); only one the new edges cut through is cut to them and set
        // in the first free cells.
        var shown = parts.Where(p => !p.Hidden).OrderBy(p => p.Y).ThenBy(p => p.X).ToList();
        var placed = shown.Where(p => p.X + p.W <= w && p.Y + p.H <= h).Select(AsCard).ToList();
        foreach (var part in shown.Where(p => p.X + p.W > w || p.Y + p.H > h))
        {
            var cut = new CardPart(part.Key, -1, -1, Math.Min(part.W, w), Math.Min(part.H, h));
            placed.Add(DashboardLayout.FirstFit(placed, AsCard(cut), w));
        }

        return AsParts(placed, parts);
    }

    /// <summary>A part standing in for a card, so the one layout engine serves inside a card too.</summary>
    private static PlacedCard AsCard(CardPart part) =>
        new(new DashboardCard(part.Key, part.Key, part.W, part.H), part.X, part.Y, part.W, part.H);

    /// <summary>The parts back from the engine, each with the type size it had in <paramref name="from"/> (Medium for one that had none), and the hidden ones of <paramref name="from"/> after them, untouched.</summary>
    private static IReadOnlyList<CardPart> AsParts(IEnumerable<PlacedCard> placed, IReadOnlyList<CardPart>? from)
    {
        var shown = placed.OrderBy(p => p.Y).ThenBy(p => p.X)
            .Select(p => new CardPart(p.Card.Id, p.X, p.Y, p.W, p.H, from?.FirstOrDefault(part => part.Key == p.Card.Id)?.Size ?? TypeSize.Medium))
            .ToList();
        return [.. shown, .. (from ?? []).Where(part => part.Hidden && shown.All(s => s.Key != part.Key))];
    }

    /// <summary>
    /// The layout an edit starts from: the cards as they stand, with every
    /// place written down, a card that had none included, so that what the
    /// user sees is what the edit changes. The columns the view was last
    /// arranged at are kept with it, as the stored form always had them: a
    /// client of an earlier build places by them.
    /// </summary>
    private List<PlacedCard> Editable()
    {
        var placed = DashboardLayout.Place(Arranged.Cards, Math.Max(1, Columns)).ToList();
        Arranged.Reference = Math.Max(Columns, DashboardLayout.Extent(placed));
        return placed;
    }

    private Task ApplyAsync(IEnumerable<PlacedCard> placed)
    {
        Arranged.Cards = [.. placed.OrderBy(p => p.Y).ThenBy(p => p.X).Select(p => p.Card with { X = p.X, Y = p.Y })];
        return ChangedAsync();
    }

    /// <summary>One card changed in itself (a setting, its layout), its place untouched.</summary>
    private Task ReplaceAsync(DashboardCard card)
    {
        Arranged.Cards[Arranged.Cards.FindIndex(c => c.Id == card.Id)] = card;
        return ChangedAsync();
    }

    private Task ChangedAsync()
    {
        Changed?.Invoke();
        return SaveAsync();
    }

    private static string NewId() => Guid.NewGuid().ToString("N");

    /// <summary>A card of a kind that is always on the dashboard (<see cref="CardKind.IsFixed"/>).</summary>
    private static bool IsFixed(DashboardCard card) => DashboardCatalogue.Find(card.Card) is { IsFixed: true };

    /// <summary>
    /// A view's arrangement with every card that is always there: one stored
    /// before such a card existed, or by a build that let it go, gets it back at
    /// the end, at the size its kind ships at in that view, where it takes the
    /// first free cells and moves nothing placed.
    /// </summary>
    private List<DashboardCard> WithFixed(List<DashboardCard> cards, DashboardView view)
    {
        foreach (var kind in DashboardCatalogue.Standard.Where(kind => kind.IsFixed && cards.All(card => card.Card != kind.Key)))
        {
            cards.Add(NewCard(kind, view));
        }

        return cards;
    }

    /// <summary>A card of a kind about to join a view: at the size the kind ships at there, or the catalogue's when it ships none.</summary>
    private DashboardCard NewCard(CardKind kind, DashboardView view) =>
        DefaultOf(kind.Key, view) is { } shipped
            ? new DashboardCard(NewId(), kind.Key, shipped.W, shipped.H)
            : new DashboardCard(NewId(), kind.Key, kind.W, kind.H);

    private static List<DashboardCard> Defaults() =>
        [.. DashboardCatalogue.All.Select(kind => new DashboardCard(NewId(), kind.Key, kind.W, kind.H))];

    private async Task LoadAsync()
    {
        // The cards' defaults first: how a card nobody has laid out stands
        // depends on them, from the first time the dashboard is drawn.
        await ReadDefaultsAsync();
        try
        {
            Scope = await js.InvokeAsync<string?>("wslcAgent.dashboardScope") == Device ? DashboardScope.Device : DashboardScope.User;
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or TaskCanceledException)
        {
            _loading = null;  // The web view was not there yet: the next dashboard asks again.
            return;
        }

        var stored = await ReadAsync(Scope);
        if (stored is { Length: > 0 })
        {
            _known = stored;
            (_desktop, _mobile) = Parse(stored);
            Changed?.Invoke();
        }
    }

    /// <summary>What is kept where, as its own text; null when this device could not read it, empty when there is none.</summary>
    private async Task<string?> ReadAsync(DashboardScope scope)
    {
        try
        {
            return scope == DashboardScope.User
                ? await api.GetUserDashboardAsync()
                : await js.InvokeAsync<string?>("wslcAgent.dashboard") ?? "";
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or TaskCanceledException or AgentApiException or HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>
    /// The stored arrangement, or what an older build stored: a comma-separated
    /// list of keys, a bare array of cards, or an object of an earlier form;
    /// all read as the kinds in their order, at the default size and not yet
    /// placed, which flow until the first edit places them. A card whose kind
    /// this build does not know is dropped, not drawn empty; a size beyond the
    /// limits is kept within them, and parts that name what the kind has not
    /// are dropped; a value that is none of these forms leaves the default set
    /// standing.
    /// </summary>
    private (Arrangement Desktop, Arrangement? Mobile) Parse(string stored)
    {
        var (desktop, mobile) = ParseStored(stored);
        desktop.Cards = WithFixed(desktop.Cards, DashboardView.Desktop);
        if (mobile is not null)
        {
            mobile.Cards = WithFixed(mobile.Cards, DashboardView.Mobile);
        }

        return (desktop, mobile);
    }

    /// <summary>The stored text read into its two arrangements, every form an earlier build wrote included (<see cref="Parse"/>).</summary>
    private static (Arrangement Desktop, Arrangement? Mobile) ParseStored(string stored)
    {
        var text = stored.TrimStart();
        if (!text.StartsWith('[') && !text.StartsWith('{'))
        {
            return (new Arrangement(0, [.. stored.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(key => DashboardCatalogue.Find(key) is not null)
                .Distinct(StringComparer.Ordinal)
                .Select(key => new DashboardCard(NewId(), key))]), null);
        }

        try
        {
            var parsed = text.StartsWith('[')
                ? new Stored(0, JsonSerializer.Deserialize<List<DashboardCard>>(text, Json) ?? [])
                : JsonSerializer.Deserialize<Stored>(text, Json) ?? new Stored(0, []);
            var known = Known(parsed.Cards);
            // The half cell of the afternoon (version 3) is two quarter cells each
            // way: that arrangement is read with every number doubled and looks
            // as it did (the owner, 22 September 2026), and is written in the
            // new form on its next save. Anything older starts again. Neither
            // had a mobile arrangement.
            if (parsed.Version == HalfCellVersion)
            {
                return (new Arrangement(Math.Max(0, parsed.Columns) * 2, [.. known.Select(Doubled).Select(Sane)]), null);
            }

            if (parsed.Version < FormVersion)
            {
                return (new Arrangement(0, [.. known.Select(Fresh)]), null);
            }

            var mobile = parsed.Mobile is { } view ? new Arrangement(Math.Max(0, view.Columns), [.. Known(view.Cards).Select(Sane)], view.Status) : null;
            return (new Arrangement(Math.Max(0, parsed.Columns), [.. known.Select(Sane)], parsed.Status), mobile);
        }
        catch (JsonException)
        {
            return (new Arrangement(0, Defaults()), null);
        }
    }

    /// <summary>The stored cards this build knows, each once.</summary>
    private static IEnumerable<DashboardCard> Known(IEnumerable<DashboardCard>? cards) =>
        (cards ?? [])
            .Where(card => card is { Id.Length: > 0, Card.Length: > 0 } && DashboardCatalogue.Find(card.Card) is not null)
            .DistinctBy(card => card.Id, StringComparer.Ordinal);

    /// <summary>
    /// The desktop's cards for a mobile view shown for the first time: each
    /// with an id of its own, flowing in reading order until placed; a card
    /// nobody has laid out at the size its kind ships at on mobile, so it fits
    /// the view's columns (the System card is nine wide on the desktop, eight
    /// on mobile).
    /// </summary>
    private Arrangement Flowed(Arrangement desktop) =>
        new(0, [.. desktop.Cards.Select(card => card with
        {
            Id = NewId(),
            X = -1,
            Y = -1,
            W = card.Parts is null && DefaultOf(card.Card, DashboardView.Mobile) is { } shipped ? shipped.W : card.W,
            H = card.Parts is null && DefaultOf(card.Card, DashboardView.Mobile) is { } tall ? tall.H : card.H,
        })]);

    /// <summary>A card placed in half cells, in quarter cells: its place, its size and its parts' places and sizes twice what they were, a place not yet found left as it was.</summary>
    private static DashboardCard Doubled(DashboardCard card) => card with
    {
        X = Twice(card.X),
        Y = Twice(card.Y),
        W = card.W * 2,
        H = card.H * 2,
        Parts = card.Parts?.Select(part => part with { X = Twice(part.X), Y = Twice(part.Y), W = part.W * 2, H = part.H * 2 }).ToList(),
    };

    private static int Twice(int place) => place < 0 ? place : place * 2;

    /// <summary>A card of an earlier form: its id, its kind and what it points at kept, its size its kind's and its place to be found again.</summary>
    private static DashboardCard Fresh(DashboardCard card)
    {
        var kind = DashboardCatalogue.Find(card.Card)!;
        return new DashboardCard(card.Id, card.Card, kind.W, kind.H, Ref: card.Ref);
    }

    /// <summary>
    /// A stored card kept within what this build allows: its size within the
    /// limits, its parts those of its kind. Parts that stand inside the card
    /// stay exactly where the user put them, holes included; only parts that
    /// would stand outside it are placed again.
    /// </summary>
    private static DashboardCard Sane(DashboardCard card)
    {
        // Only the shape is checked here: the width against the columns there
        // are is checked when the card is placed, which is where they are known.
        var w = Math.Max(1, card.W);
        var h = DashboardCatalogue.ClampRows(card.H);
        var keys = DashboardCatalogue.Find(card.Card)!.Parts;
        var parts = card.Parts?.Where(part => keys.Contains(part.Key) && part is { W: >= 1, H: >= 1, X: >= 0, Y: >= 0 }).DistinctBy(part => part.Key).ToList();
        if (parts is not { Count: > 0 })
        {
            return card with { W = w, H = h, Parts = null };
        }

        var inside = parts.All(part => part.Hidden || (part.X + part.W <= w && part.Y + part.H <= h));
        return card with { W = w, H = h, Parts = WithNewParts(card.Card, inside ? parts : Refit(parts, w, h) ?? parts, w) };
    }

    /// <summary>
    /// A card laid out before its kind gained a part (the owner, 24 September
    /// 2026: the System card's body became one part per reading) gets that part
    /// in the first free cells, at its defined size, nothing laid out moving
    /// for it; one the card has no room for waits below its last row, as a
    /// part always does, until the card is given the rows.
    /// </summary>
    private static List<CardPart> WithNewParts(string kind, IReadOnlyList<CardPart> parts, int columns)
    {
        var missing = (DashboardCatalogue.Find(kind)?.Parts ?? []).Where(key => parts.All(part => part.Key != key)).ToList();
        if (missing.Count == 0)
        {
            return [.. parts];
        }

        var placed = parts.Where(part => !part.Hidden).Select(AsCard).ToList();
        foreach (var key in missing)
        {
            var (pw, ph) = DashboardCatalogue.PartSize(kind, key);
            placed.Add(DashboardLayout.FirstFit(placed, AsCard(new CardPart(key, -1, -1, Math.Min(pw, columns), ph)), columns));
        }

        return [.. AsParts(placed, parts)];
    }

    private Task SaveAsync()
    {
        _known = Text;
        return WriteAsync(Scope, _known);
    }

    /// <summary>Both arrangements as they are stored, wherever they are stored.</summary>
    private string Text => TextOf(_ => true);

    /// <summary>Both arrangements without the cards of this machine's resources: what the shipped default may carry. The cells those stood in stay empty.</summary>
    private string SystemText => TextOf(card => DashboardCatalogue.Find(card.Card) is { IsResource: false });

    private string TextOf(Func<DashboardCard, bool> keep) =>
        JsonSerializer.Serialize(
            new Stored(
                _desktop.Reference,
                [.. _desktop.Cards.Where(keep)],
                FormVersion,
                _mobile is null ? null : new StoredView(_mobile.Reference, [.. _mobile.Cards.Where(keep)], StatusOf(_mobile, keep)),
                StatusOf(_desktop, keep)),
            Json);

    /// <summary>A view's bar alarms whose card is kept, or null when there are none, so a dashboard without them stores none.</summary>
    private static List<StatusAlarm>? StatusOf(Arrangement view, Func<DashboardCard, bool> keep)
    {
        var kept = view.Status.Where(status => view.Cards.Any(card => card.Id == status.Card && keep(card))).ToList();
        return kept.Count > 0 ? kept : null;
    }

    private async Task WriteAsync(DashboardScope scope, string text)
    {
        try
        {
            if (scope == DashboardScope.User)
            {
                await api.SetUserDashboardAsync(text);
            }
            else
            {
                await js.InvokeAsync<string?>("wslcAgent.dashboard", text);
            }
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or TaskCanceledException or AgentApiException or HttpRequestException)
        {
            // Storage refused, the agent unreachable or the web view gone: the
            // arrangement still holds for this run, and the next change tries again.
        }
    }
}
