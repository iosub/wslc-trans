namespace WslcAgent.ApiClient.Contracts;

/// <summary>Body of <c>GET /api/v1/dashboard/card-defaults</c>: how each kind of dashboard card ships.</summary>
/// <param name="Writable">This agent writes them back to its repository (a development build), so the card Settings offer Save as default.</param>
/// <param name="Defaults">The client's own text, as it wrote it: each view's kinds, each with its size and its parts. Empty when none ship.</param>
public sealed record CardDefaultsResponse(bool Writable, string Defaults);

/// <summary>Body of <c>GET /api/v1/dashboard/object-defaults</c>: how each kind of Home v2's object is born, view by view.</summary>
/// <param name="Writable">This agent writes them back to its repository (a development build), so the board of every object can design them.</param>
/// <param name="Defaults">The client's own text, as it wrote it: each view's object types, each with its cells and its look. Empty when none ship.</param>
public sealed record ObjectDefaultsResponse(bool Writable, string Defaults);
