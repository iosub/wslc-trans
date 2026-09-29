# Architecture

## Goal

One product on one stack: a Windows agent written in .NET that manages WSLC
through the `wslc` CLI and exposes it three ways, all from the same code base:

1. a web dashboard (Blazor WebAssembly, served by the agent, installable as a
   PWA),
2. native Windows and Android apps (MAUI Blazor Hybrid hosting the same Razor
   components),
3. an MCP server (official C# SDK, hosted in the agent process).

## Projects

```
src/
  WslcAgent.Server     ASP.NET Core host: wslc runner, /api/v1, static UI, /mcp. Exe: wslc-ai-agent.exe
  WslcAgent.UI         The UI: Razor Class Library of MudBlazor components (layout, pages).
  WslcAgent.Web        Blazor WebAssembly host of UI, served by Server; PWA manifest + service worker.
  WslcAgent.App        MAUI Blazor Hybrid host of UI: Windows (wslc-ai-client.exe) and Android (ai.berpiztu.wslcagent).
  WslcAgent.ApiClient  Contracts (records) and a typed HttpClient for /api/v1.
  WslcAgent.Mcp        MCP tool types and the interfaces they need from the host.
tests/
  WslcAgent.Server.Tests   Integration tests over WebApplicationFactory.
packaging/
  agent-install/, client-install/   WiX 5 projects for the two per-user MSIs.
  Packaging.ps1                     Helpers used by the root build-*.ps1 scripts.
```

Dependency direction: `Server -> Web, Mcp, ApiClient`; `Web -> UI`;
`App -> UI, ApiClient`; `UI -> ApiClient`; `Mcp -> ApiClient`. `Mcp`
declares the service interfaces it needs (`IAgentInfo`, `IContainerService`,
…) and the server implements them, so tools and endpoints share one service
layer and return the same contract records.

## Rules that shape the code

- **One clean `/api/v1`.** Every screen and every MCP tool goes through it;
  `docs/api-v1.md` is the contract and grows with each slice. The reference
  implementation's HTTP surface (per-page routes such as `/containers/api`)
  is not reproduced: its clients are retired with it, and only its behaviour
  and data are ported.
- **One UI.** `WslcAgent.UI` is the only user interface. The hosts add
  nothing visual: `Web` registers services and routes; `App` does the same in
  a WebView and adds native code only for connection settings, login,
  self-update (`IClientUpdates`) and saving a file on the user's machine
  (`IClientFiles`: Windows `FileSavePicker`, Android's Storage Access
  Framework, because a WebView has no download UI). Each is an interface the
  shared UI asks, with a no-op browser implementation.
- **MudBlazor's look, not a copy of the old one.** Layout, tables, dialogs,
  snackbars and forms are MudBlazor components on a MudBlazor theme. There is
  no custom CSS beyond the theme and the boot-time loading screen. JavaScript
  interop is limited to the terminal (xterm.js), charts, the network map and
  the host folder picker.
- **Layout.** Wide (md and up): menu button and drawer on the left, drawer
  open. Narrow (sm and down): the drawer becomes an overlay and a menu button
  appears in the top bar, on the left as everywhere else — unless the host is
  hand-held (a coarse pointer: phone or tablet, including the Android client),
  where button and drawer move to the right, within the thumb's reach. A small
  window on a mouse host is not hand-held.
- **No internet dependencies at run time.** The UI must work on an offline LAN,
  so no CDN fonts or scripts; MudBlazor ships its own assets, and the terminal
  emulator is vendored under `src/WslcAgent.UI/wwwroot/vendor/xterm`
  (xterm.js, its fit addon, its stylesheet and its licence), loaded by
  `wslc-terminal.js` the first time a terminal opens. That module also keeps
  the tab's terminal sessions across navigation: a page that leaves detaches
  its surface, the shell on the agent stays connected, and the page that
  comes back takes it over. Log out closes them all.
- **Secrets never enter the repository.** Per-machine settings live outside
  the checkout, the Android signing key lives in the user profile; the history
  must be publishable at any time.
- **Static assets and environments.** `dotnet run` (Development) serves the
  referenced projects' assets from the static web assets manifest; a
  Production run must come from `dotnet publish`, which copies them into
  `wwwroot`. Do not add `UseBlazorFrameworkFiles` next to `MapStaticAssets`:
  it looks for unfingerprinted file names and fails.
- **Installed agent settings.** The MSI stores bind host and port under
  `HKCU\Software\Berpiztu\wslc-agent\Agent`; the agent reads them when no
  URL was given explicitly. wslc only works inside an interactive user
  session, so the MSI registers a per-user logon task that starts the agent
  at install time and at every logon; never a Windows service.

## Versioning

Two independent versions, both bumped by the packaging scripts:

- Agent: the single `<Version>` in `Directory.Build.props`, reported in
  `/api/v1/health`, in the MCP `serverInfo` and in the UI. `build-agent-installer.ps1`
  bumps the patch.
- Client: `ApplicationDisplayVersion` (Windows ProductVersion, Android
  versionName) and `ApplicationVersion` (Android versionCode) in
  `src/WslcAgent.App`. `build-client-installer.ps1` and `build-client-apk.ps1`
  bump both.

## Diagrams

Three interactive diagrams, made with the Archify skill from the JSON next to
each and served by the UI at `_content/WslcAgent.UI/archify/`, under
`src/WslcAgent.UI/wwwroot/archify/`. The Architecture button at the right end
of the bottom strip opens the screen's own: the control plane everywhere, the
terminal one on Terminal and on Containers (a container's shell is opened
there), the network one on Networks, the skill and MCP one on Settings; the
window's title row switches between them. They describe the product, never one
deployment: no host names, addresses or certificates.

| File | What it shows |
|---|---|
| `wslc-overview` | The control plane: clients, the agent (API, MCP, terminals, host browser, restart policies), wslc, the containers, an optional public host; how one code base compiles into the agent and the apps. |
| `wslc-terminal` | The terminals: xterm.js in the UI, the exec protocol over one WebSocket per shell, a ConPTY on the agent (pipes without one), the host shell or `wslc exec` into a container, sessions that outlive the page, Windows Terminal at the agent's own machine. |
| `wslc-networks` | The two ways to a container's page: the host browser with nothing to set up, and the published proxy (an nginx container whose map the agent writes) behind an optional public host. |
| `wslc-mcp` | The wslc skill in Hermes, OpenClaw and Claude Code, the MCP endpoint, the tools, the approval gate for destructive tools and the operator's switches. |

To change one, edit its `.architecture.json`, then validate and deliver it with
the skill at showcase quality (`archify validate` / `deliver` / `visual-check`);
the HTML is the delivered artifact, never edited by hand.

## Reference implementation

The previous product (Python agent, Jinja web UI, MAUI XAML clients) is the
functional specification: which screens exist, what they show, which actions
they offer and with which confirmations, how `wslc` output is parsed, and the
MCP tool list with its approval model. Behaviour is ported from it; neither
its look nor its code is copied, with the exception of its C# API client,
which may be brought over and renamed.

It keeps moving, so `docs/changes-refe.md` holds **the last reference commit
already ported here**. Read that file before looking for what is missing: what
is pending is exactly what `git -C ../wslc-dashboard log <that id>..HEAD`
lists, and the file is updated in the same commit that ports it.

## Phases

| Phase | Deliverable | Validated by |
|---|---|---|
| 0 | Solution skeleton: Server serves UI (PWA), `/api/v1/health`, MCP `health` tool, App hosts UI on Windows and Android, installers and APK build, CI. **Done.** | `dotnet test`, headless browser render, app launch, `dist/` artifacts |
| 1 | Server with the full `/api/v1` and the MCP tools, running `wslc`; the UI page for each resource lands with its endpoints (containers list/start/stop/restart done). | Smoke tests over `FakeWslcRunner`, the user's review of each slice |
| 2 | UI pages reaching functional parity with the reference UI. | Manual parity checklist per page |
| 3 | App: connection settings, login, self-update; installer settings dialogs. | Install and rotate on a device |
| 4 | Reference implementation retired. | |
