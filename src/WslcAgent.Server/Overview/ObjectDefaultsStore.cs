namespace WslcAgent.Server.Overview;

/// <summary>
/// How each kind of Home v2's object is born, view by view — its cells, its
/// type size, its alignment and margins, its colours and its parts
/// (docs/home/v2/specv2.md, decision 33): what an object dropped from the
/// toolbox takes and what Reset brings back. Designed on the development
/// agent on the board of every object (the owner, 26 September 2026), and
/// written back to the repository (object-defaults.json).
/// </summary>
public sealed class ObjectDefaultsStore(ILogger<ObjectDefaultsStore> logger)
    : ShippedDefaults(new ShippedFile("object-defaults.json", "ObjectDefaultsSource"), logger);
