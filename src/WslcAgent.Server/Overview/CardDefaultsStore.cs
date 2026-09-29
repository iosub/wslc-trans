namespace WslcAgent.Server.Overview;

/// <summary>
/// How each kind of dashboard card ships, view by view — its size and how its
/// parts stand inside it (docs/home/spec.md, section 4): what a card added
/// from the + list opens at, what a card nobody has laid out shows, and what
/// Reset layout brings back. Designed on the development agent, a card at a
/// time, with the card's Settings' Save as default (the owner, 24 September
/// 2026), and written back to the repository (card-defaults.json).
/// </summary>
public sealed class CardDefaultsStore(ILogger<CardDefaultsStore> logger)
    : ShippedDefaults(new ShippedFile("card-defaults.json", "CardDefaultsSource"), logger);
