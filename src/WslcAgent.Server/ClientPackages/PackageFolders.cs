using Microsoft.Extensions.Options;
using WslcAgent.Server.Wslc;

namespace WslcAgent.Server.ClientPackages;

/// <summary>
/// Where the installers the agent hands out live: the clients' MSI and APK for
/// the clients to update themselves, and the agent's own MSI for the agent to.
/// One place for all of them, because one copy from the build machine
/// (deploy-server.ps1) puts all three there.
/// </summary>
public sealed class PackageFolders(IOptions<WslcOptions> options, IHostEnvironment environment)
{
    /// <summary>
    /// Every machine that builds the clients keeps the checkout here and the
    /// build scripts publish to its <c>dist</c>; an installed agent runs from
    /// %LOCALAPPDATA% with no <c>dist</c> of its own, so this is where it looks
    /// when <c>Wslc:ClientPackagesPath</c> is not set.
    /// </summary>
    private const string BuildMachineDist = @"C:\IA\wslc\wslc-agent\dist";

    /// <summary>The file by that name in the first folder that has it, or null.</summary>
    public string? Locate(string filename) =>
        Folders().Select(folder => Path.Combine(folder, filename)).FirstOrDefault(File.Exists);

    /// <summary>Where a package may live, first match wins: the configured folder, the build machine's <c>dist</c>, the agent's own <c>dist</c>.</summary>
    private IEnumerable<string> Folders()
    {
        var configured = options.Value.ClientPackagesPath;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            yield return Environment.ExpandEnvironmentVariables(configured);
        }

        yield return BuildMachineDist;
        yield return Path.Combine(environment.ContentRootPath, "dist");
    }
}
