namespace WslcAgent.UI.Components;

/// <summary>A card on the grid: the cell its top-left corner is in and the cells it spans.</summary>
public sealed record PlacedCard(DashboardCard Card, int X, int Y, int W, int H)
{
    public bool Overlaps(PlacedCard other) =>
        X < other.X + other.W && other.X < X + W && Y < other.Y + other.H && other.Y < Y + H;
}

/// <summary>
/// Where the dashboard's cards stand (docs/home/spec.md, section 4). A card
/// is placed by cell — the column and the row the user dragged it to — so two
/// wide cards can stand one under the other with room beside them, and a hole
/// stays where the user left one. It stays there, at its size, whatever the
/// width of the window (the owner, 22 September 2026): a window too narrow
/// for the cards scrolls sideways, and nothing is placed again. Nothing here
/// touches the DOM; the page draws what this computes.
/// </summary>
public static class DashboardLayout
{
    /// <summary>
    /// The cards placed for a grid that shows <paramref name="columns"/>. A
    /// card with a place stands in it at its own size, whatever the grid shows
    /// (the owner, 22 September 2026: every view keeps its cards where the
    /// user put them, and the dashboard scrolls both ways instead). A card with
    /// no place yet — one just added, or a view shown for the first time —
    /// takes the first free cells among them, in the columns shown, so its
    /// arrival moves nothing.
    /// </summary>
    public static IReadOnlyList<PlacedCard> Place(IReadOnlyList<DashboardCard> cards, int columns)
    {
        // Not measured yet: spans alone, each card at its own size, and the grid
        // flows them itself until it is. Never the default size (the owner,
        // 22 September 2026): a resize begun then would begin from it.
        if (columns <= 0)
        {
            return [.. cards.Select(card => Span(card, card.W))];
        }

        var placed = cards.Where(card => card.X >= 0).Select(card => Span(card, card.W) with { X = card.X, Y = card.Y }).ToList();
        foreach (var card in cards.Where(card => card.X < 0))
        {
            placed.Add(FirstFit(placed, Span(card, columns), columns));
        }

        return placed;
    }

    /// <summary>The columns the placed cards reach: as many as the grid needs, beyond those the window shows when it is too narrow for them.</summary>
    public static int Extent(IEnumerable<PlacedCard> placed) => placed.Select(p => p.X + p.W).DefaultIfEmpty(0).Max();

    /// <summary>The first cells from row <paramref name="fromY"/> down, row by row, where <paramref name="card"/> fits among <paramref name="placed"/>.</summary>
    public static PlacedCard FirstFit(IReadOnlyList<PlacedCard> placed, PlacedCard card, int columns, int fromY = 0)
    {
        for (var y = Math.Max(0, fromY); ; y++)
        {
            for (var x = 0; x + card.W <= Math.Max(columns, card.W); x++)
            {
                var candidate = card with { X = x, Y = y };
                if (!placed.Any(candidate.Overlaps))
                {
                    return candidate;
                }
            }
        }
    }

    /// <summary>
    /// Whether <paramref name="candidate"/> stands on free cells: it overlaps
    /// nothing in <paramref name="placed"/> but itself. The one rule for every
    /// move and resize, of a card or of a part (the owner, 22 September 2026):
    /// an item goes only where there is room, and nothing else ever moves to
    /// make it — the user makes the room.
    /// </summary>
    public static bool IsFree(IEnumerable<PlacedCard> placed, PlacedCard candidate) =>
        !placed.Any(p => p.Card.Id != candidate.Card.Id && p.Overlaps(candidate));

    /// <summary>A card with its span, kept within the limits and the columns there are, and no place yet.</summary>
    public static PlacedCard Span(DashboardCard card, int columns) =>
        new(card, -1, -1, DashboardCatalogue.ClampColumns(card.W, columns), DashboardCatalogue.ClampRows(card.H));
}
