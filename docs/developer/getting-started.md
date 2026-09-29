# Getting started

From a Windows machine with nothing of this project on it to the agent
running from source, the tests green and, if you want them, the installers
built. Every step says what you should see; when you see something else, the
step says what to do.

The short version is in the [README](../../README.md#quick-start). This is
the same path, one step at a time.

## 1. Allow PowerShell to run the repository's scripts

Everything is driven by PowerShell scripts at the root of the repository.
Windows PowerShell refuses to run scripts until you allow it, once, for your
user:

```powershell
Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
```

Answer `Y`. PowerShell 7 (`pwsh`) already allows it; the scripts work in
both.

## 2. Clone the repository

```powershell
git clone https://github.com/berpiztu/wslc-ai-agent.git
cd wslc-ai-agent
```

No git yet? `winget install --id Git.Git -e`, then open a new terminal.

## 3. Check the prerequisites

```powershell
.\check-prereqs.ps1
```

It looks at what this machine has and changes nothing. Each line is one of:

- `[ok]`: present.
- `[absent]`: missing, and only the part it names is switched off (the APK,
  the Windows client, containers to manage). The line says how to install it.
- `[broken]`: missing, and nothing builds without it. The line gives the
  command that installs it.

What you need at the least, and how to install it:

| Prerequisite | Install |
|---|---|
| Windows 11 (Windows 10 builds everything, but WSLC runs on Windows 11) | — |
| The .NET SDK that `global.json` names, or a later feature band of it | `winget install --id Microsoft.DotNet.SDK.10 -e` |
| The `maui-windows` and `android` workloads | `dotnet workload restore WslcAgent.slnx` (run in the repository) |
| Access to nuget.org (the first build downloads every package, WiX included) | — |

And for the parts that need them:

| Prerequisite | For | Install |
|---|---|---|
| Android SDK | The APK, `debug-android.ps1` | Installed by the `android` workload on the first Android build, or with Android Studio; elsewhere, set `ANDROID_HOME` |
| A JDK (`keytool`) | The APK and its signing key | `winget install --id Microsoft.OpenJDK.21 -e`, or set `JAVA_HOME` |
| WebView2 Runtime | The Windows client, the tray window | `winget install --id Microsoft.EdgeWebView2Runtime -e` |
| WSLC **2.9.13 or later** (`wslc` on the PATH) | The agent's containers | [WSL 2.9.13](https://github.com/microsoft/WSL/releases/tag/2.9.13) or a later release, then open a new terminal |

The agent works with WSLC 2.9.13 or later; an older `wslc` is reported as
`[broken]` with the link to update it. `wslc version` prints the one you have.

Install what is marked `[broken]`, open a new terminal, and run the script
again until the last line is green: **Ready to build**.

## 4. Check the private files (optional)

```powershell
.\check-private.ps1
```

The signing key of the Android app and the two Firebase files of push
notifications are secrets, so they are not in the repository. A fresh clone
has none of them, and that is fine: everything builds, runs and tests
without them. On a fresh clone you should see every file `[absent]` and the
last line green: **Nothing broken**.

When you want signed APKs or push notifications,
[private-files.md](private-files.md) says how to make each file, and this
script tells you whether each one is right.

## 5. Build and test

```powershell
.\build.ps1
```

It restores the packages, builds the whole solution and runs the tests: the
same steps CI runs. The first run downloads the packages and takes a few
minutes. You should see `Restoring`, `Building`, `Testing`, the test summary
with no failure, and **Done.** in green.

`.\build.ps1 -Configuration Release` matches CI exactly.

## 6. Run the agent

```powershell
.\start-agent.ps1
```

It builds and starts the agent on <http://127.0.0.1:8070>, with its icon
beside the clock. Open that address in a browser: the Home dashboard shows,
and Containers, Images, Volumes and Networks list what WSLC has. Stop it with
`Ctrl+C`.

`-Watch` reloads the agent on every save; `-Port` changes the port;
`-NoTray` leaves the icon out.

## 7. Connect an AI assistant (optional)

The agent serves an MCP endpoint at `/api/v1/mcp`. With the agent running,
from Claude Code:

```powershell
claude mcp add --transport http wslc-agent http://127.0.0.1:8070/api/v1/mcp
```

Destructive tools ask for your approval before they act.

## 8. Run the clients (optional)

With the agent running, in another terminal:

```powershell
.\debug-client.ps1      # the Windows client, pointed at http://127.0.0.1:8070/
.\debug-android.ps1     # the Android client on an emulator, or a phone with -Phone
```

`debug-android.ps1` starts an emulator when no device is attached (create one
in Android Studio's Device Manager first). From the emulator, the PC is
`10.0.2.2`, which is the default agent address it passes.

## 9. Build the installers (optional)

```powershell
.\build-agent-installer.ps1 -NoBump     # dist\wslc-ai-agent.msi
.\build-client-installer.ps1 -NoBump    # dist\wslc-ai-client.msi
.\build-client-apk.ps1 -NoBump          # dist\wslc-ai-client.apk
```

`-NoBump` builds the current version. Without it each script raises the
version number in the project files, which is how a release is made; for
trying the installers, leave the version alone.

The agent MSI installs per user under `%LOCALAPPDATA%\WSLC-AI-Agent`, runs on
<http://127.0.0.1:8069> by default and starts at every logon. The APK is
signed with your key from `private\` (generated there the first time), or
see [private-files.md](private-files.md).

## When something fails

1. Run `.\check-prereqs.ps1` again: most failures are a missing SDK or
   workload.
2. A build that fails after an SDK or workload change: close the terminal,
   open a new one, and build again.
3. Still failing: open an issue with the output of both check scripts (they
   print no secret) and of the failing command.
