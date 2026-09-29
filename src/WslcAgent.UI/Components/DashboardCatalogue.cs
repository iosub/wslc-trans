namespace WslcAgent.UI.Components;

/// <summary>
/// One kind of card the dashboard can show (docs/home/spec.md, section 3):
/// its key in storage, the word the + list shows for it, and the page its
/// Open verb goes to (empty for a reading that opens nothing, or a chart,
/// which opens its own dialog; a resource card opens its family's page).
/// A standard kind opens at two cells by two, the box the dashboard always
/// had; a resource card at the size its card needs. Either takes any size
/// after that.
/// </summary>
public sealed record CardKind(string Key, string Label, string Href = "", int W = DashboardCatalogue.DefaultColumns, int H = DashboardCatalogue.DefaultRows)
{
    /// <summary>A chart card: Open shows the full chart instead of a page.</summary>
    public bool IsChart => Key.EndsWith("-chart", StringComparison.Ordinal);

    /// <summary>A card that shows one of the user's resources, added from the + list, not a reading of the whole host.</summary>
    public bool IsResource => DashboardCatalogue.Resources.Any(kind => kind.Key == Key);

    /// <summary>
    /// The objects the card is made of, each laid out inside it by the user
    /// (docs/home/spec.md, section 4): the summary (its three texts) on every
    /// card; the chart and its legend on a chart card; the bars on the
    /// session stores; the list card's header, details and verbs on a
    /// resource card.
    /// </summary>
    public IReadOnlyList<string> Parts =>
        IsChart ? [DashboardCatalogue.Summary, DashboardCatalogue.Chart, DashboardCatalogue.Legend]
        : Key == "storage-sessions" ? [DashboardCatalogue.Summary, DashboardCatalogue.Bars]
        : Key == DashboardCatalogue.Transfers ? [DashboardCatalogue.Summary, DashboardCatalogue.Details]
        : Key == DashboardCatalogue.System ? [DashboardCatalogue.Summary, .. DashboardCatalogue.SystemPieces.Select(piece => piece.Key)]
        : IsResource ? [DashboardCatalogue.Summary, DashboardCatalogue.Details, DashboardCatalogue.Actions]
        : IsAlarm ? [DashboardCatalogue.Ring]
        : [DashboardCatalogue.Summary];

    /// <summary>A card of the Alarms family: one of the host's measures as its ring alone (the owner, 22 September 2026).</summary>
    public bool IsAlarm => DashboardCatalogue.Alarms.Any(kind => kind.Key == Key);

    /// <summary>
    /// Always on the dashboard (the owner, 24 September 2026): moved and
    /// resized like any card, never taken off. The System card, which holds
    /// the client's version and update now that the title bar does not.
    /// </summary>
    public bool IsFixed => Key == DashboardCatalogue.System;
}

/// <summary>Every kind of card there is, in the order a fresh install shows them: the reference's six counters, its charts, its storage.</summary>
public static class DashboardCatalogue
{
    /// <summary>The keys of a card's parts (<see cref="CardKind.Parts"/>).</summary>
    public const string Summary = "summary";

    public const string Chart = "chart";

    /// <summary>An alarm card's one part: its measure's ring.</summary>
    public const string Ring = "ring";

    public const string Legend = "legend";

    public const string Bars = "bars";

    /// <summary>A resource card's readings and dials, its list page's card body.</summary>
    public const string Details = "details";

    /// <summary>A resource card's verbs, its list page's card footer.</summary>
    public const string Actions = "actions";

    /// <summary>The kinds of a resource card: a container (slice 2), an image, a volume, a network (slice 3).</summary>
    public const string Container = "container";

    public const string Image = "image";

    public const string Volume = "volume";

    public const string Network = "network";

    /// <summary>The Windows drive the sessions' VHDX files are on: used, its size, and its alarm (the owner, 22 September 2026).</summary>
    public const string DiskSpace = "disk-space";

    /// <summary>The files travelling in and out of every container at once, the ring's list without a container to open it from (the owner, 23 September 2026).</summary>
    public const string Transfers = "transfers";

    /// <summary>
    /// The system at a glance (the owner, 24 September 2026): the session in
    /// its header, and every reading a part of its own — the versions, the
    /// event stream, the load, the client's update or download. The client
    /// version lives here instead of beside the brand.
    /// </summary>
    public const string System = "system";

    /// <summary>The alarm cards' keys.</summary>
    public const string CpuAlarm = "alarm-cpu";

    public const string MemoryAlarm = "alarm-memory";

    public const string DiskAlarm = "alarm-disk";

    /// <summary>What an alarm watches (<see cref="CardAlarm.Measure"/>).</summary>
    public const string CpuMeasure = "cpu";

    public const string MemoryMeasure = "memory";

    public const string DiskMeasure = "disk";

    /// <summary>
    /// The measures a card of this kind can raise an alarm on (the owner,
    /// 22 September 2026): the host's CPU on the CPU reading and chart, its
    /// memory on the memory ones, the drive on Disk space; a container's own
    /// CPU or memory on a container's card. Empty: no alarm.
    /// </summary>
    public static IReadOnlyList<string> Measures(string card) => card switch
    {
        "cpu" or "cpu-chart" or CpuAlarm => [CpuMeasure],
        "memory" or "memory-chart" or MemoryAlarm => [MemoryMeasure],
        DiskSpace or DiskAlarm => [DiskMeasure],
        Container => [CpuMeasure, MemoryMeasure],
        _ => [],
    };

    /// <summary>
    /// The card's alarm on one measure as its Settings show it: what the user
    /// set, or, until they do, its kind's own — on at the default threshold on
    /// a system or alarm card, since it is on the dashboard to be watched, and
    /// off on a container's card until it is switched on.
    /// </summary>
    public static CardAlarm AlarmSetting(DashboardCard card, string measure) =>
        card.Alarms?.FirstOrDefault(alarm => alarm.Measure == measure)
        ?? new CardAlarm(measure, Off: Find(card.Card) is { IsResource: true });

    /// <summary>The card's alarms that are on, one per measure it watches.</summary>
    public static IEnumerable<CardAlarm> AlarmsOf(DashboardCard card) =>
        Measures(card.Card).Select(measure => AlarmSetting(card, measure)).Where(alarm => !alarm.Off);

    /// <summary>A measure as the status bar names it, short enough for its narrow cells: CPU, Mem, Disk.</summary>
    public static string MeasureShort(string measure) => measure switch
    {
        CpuMeasure => "CPU",
        MemoryMeasure => "Mem",
        DiskMeasure => "Disk",
        _ => measure,
    };

    /// <summary>A measure as the Settings name it.</summary>
    public static string MeasureLabel(string measure) => measure switch
    {
        CpuMeasure => "CPU",
        MemoryMeasure => "Memory",
        DiskMeasure => "Disk",
        _ => measure,
    };

    /// <summary>
    /// How many rows the size fields offer to choose from (the owner,
    /// 22 September 2026: a card is as large as the user wants it). It is not
    /// a limit — the corner handle goes past it and the field then offers the
    /// card's own height — only how far the list reaches without dragging.
    /// A card's width is limited by the columns the dashboard has, which is
    /// the room there is, not a rule.
    /// </summary>
    public const int RowsOffered = 24;

    /// <summary>
    /// The size a standard card opens at: four cells by four, the box the
    /// dashboard opened with, now that a cell is a quarter of it each way (the
    /// owner, 22 September 2026).
    /// </summary>
    public const int DefaultColumns = 4;

    public const int DefaultRows = 4;

    /// <summary>
    /// The size every part has to begin with (the owner, 22 September 2026:
    /// each object has its dimensions defined, the summary four cells by two),
    /// so a standard card shows its summary over its body. A resource
    /// card's parts span its width, since its verbs row needs all of it:
    /// its header two rows, its details four (the list card's lines and
    /// dials), its verbs one, which is what a row of buttons takes. A part
    /// given fewer cells cuts what no longer fits; nothing adjusts itself.
    /// </summary>
    public static (int W, int H) PartSize(string card, string part) =>
        Find(card) switch
        {
            // The System card: its header across it, as a resource's; each of
            // its readings the size the list gives it.
            { Key: System } system => SystemPieces.FirstOrDefault(piece => piece.Key == part) is { } piece
                ? (piece.W, piece.H)
                : (system.W, 2),
            { IsResource: true } resource => (resource.W, part switch
            {
                Details => 4,
                Actions => 1,
                _ => 2,
            }),
            { IsAlarm: true } alarm => (alarm.W, alarm.H),
            // A list wants the card's whole width and everything the summary
            // leaves under it: at the standard four by two it would be laid
            // out beside the summary, in half the card and two rows, which is
            // a box too small to read a file name in.
            { Key: Transfers } transfers => (transfers.W, part == Details ? transfers.H - 2 : 2),
            _ => (4, 2),
        };

    /// <summary>The word the card's Settings show for a part.</summary>
    public static string PartLabel(string key) => key switch
    {
        Summary => "Summary",
        Chart => "Chart",
        Legend => "Legend",
        Bars => "Bars",
        Details => "Details",
        Actions => "Actions",
        Ring => "Ring",
        _ => SystemPieces.FirstOrDefault(piece => piece.Key == key)?.Label ?? key,
    };

    /// <summary>
    /// One reading of the System card, a part of its own (the owner,
    /// 24 September 2026): what the card's Settings call it, and the cells it
    /// opens at before the user lays it out.
    /// </summary>
    public sealed record SystemPiece(string Key, string Label, int W, int H);

    /// <summary>
    /// The System card's readings, each a part the user moves, sizes, hides and
    /// sets the type size of, instead of one body that was all or nothing (the
    /// owner, 24 September 2026). More are added here: a key, what Settings
    /// call it, its cells, and what it draws in SystemHomeCard. A text reading
    /// opens half the card wide and two rows high; a ring two cells by two.
    /// </summary>
    public static readonly IReadOnlyList<SystemPiece> SystemPieces =
    [
        new(SystemSession, "Session", 8, 2),
        new(SystemClient, "Client version", 4, 2),
        new(SystemAgent, "Agent version", 4, 2),
        new(SystemWslc, "wslc version", 4, 2),
        new(SystemWindows, "Windows", 4, 2),
        new(SystemKernel, "Kernel", 4, 2),
        new(SystemEvents, "Events", 4, 2),
        new(SystemCpu, "CPU", 2, 2),
        new(SystemMemory, "Memory", 2, 2),
        new(SystemDisk, "Disk", 2, 2),
        new(SystemClientPackage, "Client download", 4, 1),
    ];

    /// <summary>The System card's reading parts' keys (<see cref="SystemPieces"/>).</summary>
    public const string SystemSession = "sys-session";

    public const string SystemClient = "sys-client";

    public const string SystemAgent = "sys-agent";

    public const string SystemWslc = "sys-wslc";

    public const string SystemWindows = "sys-windows";

    public const string SystemKernel = "sys-kernel";

    public const string SystemEvents = "sys-events";

    public const string SystemCpu = "sys-cpu";

    public const string SystemMemory = "sys-memory";

    public const string SystemDisk = "sys-disk";

    /// <summary>The native client: its download in a browser, its update in the client itself.</summary>
    public const string SystemClientPackage = "sys-client-package";

    /// <summary>The standard cards, which the + list offers: every one a reading of the whole host.</summary>
    public static readonly IReadOnlyList<CardKind> All =
    [
        new("containers", "Containers", "containers"),
        new("images", "Images", "images"),
        new("networks", "Networks", "networks"),
        new("volumes", "Volumes", "volumes"),
        new("cpu", "Aggregate CPU"),
        new("memory", "Aggregate memory"),
        new(DiskSpace, "Disk space"),
        new("cpu-chart", "CPU chart"),
        new("memory-chart", "Memory chart"),
        new("disk-chart", "Disk activity"),
        new("network-chart", "Network activity"),
        new("storage-images", "Image catalog"),
        new("storage-vhdx", "Session VHDX"),
        new("storage-swap", "Storage / swap"),
        new("storage-sessions", "Session stores"),
        // Wide enough for a file's name, its container and its cross on one
        // line, and tall enough for a handful of them: a list of one line is
        // not a list. Any size after that, as every card.
        new(Transfers, "File transfers", "containers", W: 8, H: 5),
        // The size its shipped layout fills (card-defaults.json on the agent). A second tap opens
        // the System page.
        new(System, "System", "system", W: 9, H: 4),
    ];

    /// <summary>
    /// The resources a user can add as cards, each eight cells wide, which is
    /// what the list card's verbs row needs (docs/list-pages.md, section 3:
    /// 21rem), by seven high: its header (two rows), its details (four) and its
    /// verbs (one), one over the other. Selected, it offers its family's page
    /// verbs, and no Open verb; a second tap opens its family's page (the owner,
    /// 22 September 2026).
    /// </summary>
    public static readonly IReadOnlyList<CardKind> Resources =
    [
        new(Container, "Container", "containers", W: 8, H: 7),
        new(Image, "Image", "images", W: 8, H: 7),
        new(Volume, "Volume", "volumes", W: 8, H: 7),
        new(Network, "Network", "networks", W: 8, H: 7),
    ];

    /// <summary>
    /// The Alarms family of the + list (the owner, 22 September 2026): the
    /// host's CPU, memory and drive, each as a ring alone, red and blinking
    /// past its threshold.
    /// </summary>
    public static readonly IReadOnlyList<CardKind> Alarms =
    [
        new(CpuAlarm, "CPU alarm"),
        new(MemoryAlarm, "Memory alarm"),
        new(DiskAlarm, "Disk alarm"),
    ];

    /// <summary>Every card that is not a resource's: the system cards and the alarm cards, what the + list ticks by kind.</summary>
    public static IEnumerable<CardKind> Standard => All.Concat(Alarms);

    private static readonly Dictionary<string, CardKind> ByKey =
        All.Concat(Alarms).Concat(Resources).ToDictionary(kind => kind.Key, StringComparer.Ordinal);

    public static CardKind? Find(string key) => ByKey.GetValueOrDefault(key);

    /// <summary>A width in cells kept within the columns there are: the room, not a rule.</summary>
    public static int ClampColumns(int columns, int available) => Math.Clamp(columns, 1, Math.Max(1, available));

    /// <summary>A height in cells: at least one, and as many as the user wants.</summary>
    public static int ClampRows(int rows) => Math.Max(1, rows);
}
