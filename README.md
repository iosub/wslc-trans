# WSLC AI Agent

A local agent for **WSLC** (Windows Subsystem for Linux Containers, Microsoft's
`wslc` CLI) built for the age of AI assistants: a web dashboard, Windows and
Android clients, and an MCP server so Claude and other assistants can operate
your containers, with your approval for anything destructive.

It is the small Windows process that sits next to `wslc`, talks to it for you,
and exposes what it knows over HTTP, in a browser, in native apps and through
the Model Context Protocol. Repository and namespaces are `wslc-agent`; the
products are **WSLC AI Agent** (the agent, `wslc-ai-agent.exe`) and
**WSLC AI Client** (the Windows and Android apps, `wslc-ai-client.exe`).

> **Status: early.** This repository is the .NET rebuild of an existing,
> working product. The skeleton is in place: the agent serves the UI (also
> installable as a PWA), answers `GET /api/v1/health` and the MCP `health`
> tool, the Windows and Android apps host the same UI, and the installers
> build. Features land phase by phase; see
> [docs/architecture.md](docs/architecture.md).

## What it will do

Everything the previous implementation does today, on one code base:

- Containers: list, inspect, run, create, start, stop, restart, remove, logs,
  live stats, exec, file browser, recreate with changes.
- Images: list, pull, tag, build, prune.
- Volumes and networks: list, create, inspect, connect, disconnect, prune.
- Sessions: see and switch the active WSLC session; VHDX sizes and compaction.
- Terminal: a host terminal in the browser.
- MCP server: the same operations as tools, with destructive actions gated
  behind an explicit yes/no from the user.
- Native clients for Windows and Android that host the same UI.

## Architecture

| Project | Role |
|---|---|
| `WslcAgent.Server` | ASP.NET Core agent: runs `wslc`, exposes `/api/v1`, serves the UI, hosts the MCP endpoint. Ships as `wslc-ai-agent.exe`. |
| `WslcAgent.UI` | The one user interface: Razor components on [MudBlazor](https://mudblazor.com/). |
| `WslcAgent.Web` | Blazor WebAssembly host of the UI, served by the agent, installable as a PWA. |
| `WslcAgent.App` | .NET MAUI Blazor Hybrid host of the UI for Windows and Android (`wslc-ai-client.exe`, `ai.berpiztu.wslcagent`). |
| `WslcAgent.ApiClient` | Contracts and typed client for `/api/v1`, shared by every host. |
| `WslcAgent.Mcp` | MCP tools (official C# SDK), hosted by the server. |

One UI, one language, one API contract.

## Built with

- [.NET 10](https://dotnet.microsoft.com/) and ASP.NET Core minimal APIs
- [Blazor WebAssembly](https://learn.microsoft.com/aspnet/core/blazor/) for the UI
- [MudBlazor](https://mudblazor.com/) as the component library and look
- [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) for the MCP server
- [.NET MAUI](https://learn.microsoft.com/dotnet/maui/) Blazor Hybrid for the native apps
- [WiX Toolset](https://wixtoolset.org/) for the Windows installers
- [xterm.js](https://xtermjs.org/) for the terminal (phase 2)

## Quick start

Needs Windows 11 and **WSLC 2.9.13 or later**
([WSL 2.9.13 release](https://github.com/microsoft/WSL/releases/tag/2.9.13)).
Run each line in PowerShell, one at a time, and check what it shows before
going on to the next.

**1. Let PowerShell run the scripts** (once per user; answer `Y`).

```powershell
Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
```

**2. Clone the repository.**

```powershell
git clone https://github.com/berpiztu/wslc-ai-agent.git
```

**3. Go into it.**

```powershell
cd wslc-ai-agent
```

**4. Check what this machine needs.** It installs nothing. All is well when
the last line is green: **Ready to build**. Otherwise every missing piece
names the section of
[docs/developer/prerequisites.md](docs/developer/prerequisites.md) that
installs it (VS Code, Visual Studio or no IDE); install it, open a new
terminal, and run this line again.

```powershell
.\check-prereqs.ps1
```

**5. Check the private files.** They are optional (the signing key, push
notifications) and a fresh clone has none. All is well when the last line is
green: **Nothing broken**; `[absent]` only switches off what it names.

```powershell
.\check-private.ps1
```

**6. Build and test**, as CI does. All is well when the tests pass and it
ends in **Done.** The first run downloads the packages and takes a few
minutes.

```powershell
.\build.ps1
```

**7. Run the agent.** All is well when http://127.0.0.1:8070 opens the
dashboard. `Ctrl+C` stops it.

```powershell
.\start-agent.ps1
```

Every step, what it shows and what to do when it shows something else:
[docs/developer/getting-started.md](docs/developer/getting-started.md).
The signing key and the Firebase files, when you want signed APKs or push
notifications: [docs/developer/private-files.md](docs/developer/private-files.md).

## Scripts

All scripts live in the repository root and work from any current directory.

| Script | What it does |
|---|---|
| `check-prereqs.ps1` | What the machine needs to build, test, run and package, and how to install what is missing. Changes nothing. |
| `check-private.ps1` | The private files this checkout has and what each enables, without printing a secret. |
| `build.ps1` | Restore, build, test: what CI runs. |
| `start-agent.ps1` | Build and run the agent in Development on http://127.0.0.1:8070. `-Port`, `-NoBuild`, `-Watch` (dotnet watch, hot reload on save). |
| `debug-client.ps1` | Debug build of the Windows client and launch it against `-AgentUrl` (default the local agent). |
| `debug-android.ps1` | Debug build of the Android client, deploy to an emulator or phone and launch it against `-AgentUrl` (default `http://10.0.2.2:8070/`, the host as seen from the emulator). |
| `build-agent-installer.ps1` | `dist\wslc-ai-agent.msi`: self-contained agent with the UI inside. |
| `build-client-installer.ps1` | `dist\wslc-ai-client.msi`: the Windows client. |
| `build-client-apk.ps1` | `dist\wslc-ai-client.apk`, arm64-v8a, signed. `build-client-apk-full.ps1` bundles every ABI. |

The three installer scripts raise the version in the project files, which is
how a release is made; `-NoBump` builds the current version, for trying them.

Without the scripts: `dotnet build`, `dotnet test`,
`dotnet run --project src/WslcAgent.Server` from the repository root. A
Production run must come from `dotnet publish` (or the MSI); an unpublished
Production run cannot serve the UI.

`GET /api/v1/health` reports the version; the MCP endpoint is `/api/v1/mcp`
(Streamable HTTP). To try it from Claude Code:

```powershell
claude mcp add --transport http wslc-agent http://127.0.0.1:8069/api/v1/mcp
```

The agent installer asks for the bind address and port (defaults 127.0.0.1
and 8069; development runs on 8070). Installer settings are also MSI properties, `WSLC_BINDHOST` and
`WSLC_AGENTPORT` for the agent and `WSLC_AGENTURL` for the client, e.g.
`msiexec /i dist\wslc-ai-agent.msi WSLC_AGENTPORT=8069`. The agent MSI
registers a per-user logon task that starts the agent right away and at every
logon, because wslc only works inside a user session. Android APKs are
signed with a key kept outside the repository, in `private/`; see
[docs/developer/private-files.md](docs/developer/private-files.md).

## Contributing

Issues and pull requests are welcome. The code, comments, docs and commit
messages are in English. Nothing machine-specific and no secrets go into the
repository: `.env*`, keystores and certificates are ignored on purpose.

## License

[MIT](LICENSE). Copyright (c) 2026 Berpiztu. Third-party components and
their licenses are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

WSLC AI Agent is built by [Berpiztu](https://github.com/Berpiztu), the team
behind [Virtus](https://github.com/berpiztu/virtus).
