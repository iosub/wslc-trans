# Prerequisites

What a machine needs to build, test, run and package WSLC AI Agent, and how
to install each piece. `.\check-prereqs.ps1` tells you which ones you have;
every line it reports as `[absent]` or `[broken]` points to its section here.

| Prerequisite | Needed for | Required |
|---|---|---|
| [Windows](#windows) | Everything | Yes |
| [Git](#git) | Cloning | Yes |
| [PowerShell script execution](#powershell-script-execution) | Every script of the repository | Yes |
| [.NET SDK](#net-sdk) | Building | Yes |
| [MAUI workloads](#maui-workloads) | Building the clients (the solution includes them) | Yes |
| [NuGet access](#nuget-access) | The first build | Yes |
| [WSLC 3.0.1 or later](#wslc) (2.9.13 at the least) | Running the agent against containers | To run the agent |
| [JDK](#jdk) | The Android client and its signing key | For Android |
| [Android SDK](#android-sdk) | The Android client, the emulator | For Android |
| [WebView2 Runtime](#webview2-runtime) | Running the Windows client and the tray window | For the Windows client |
| [Editor](#editor-vs-code) | Editing and debugging | No: any editor works |

Open a **new terminal** after installing anything: the PATH and environment
variables a terminal sees are the ones it started with.

## Windows

Windows 11. Windows 10 (1809, build 17763, or later) builds everything, but
WSLC runs on Windows 11 only.

## Git

```powershell
winget install --id Git.Git -e
```

Or the installer from [git-scm.com](https://git-scm.com/downloads/win).

## PowerShell script execution

Windows PowerShell refuses to run scripts until it is allowed, once per user:

```powershell
Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
```

PowerShell 7 (`pwsh`) allows it already. More in
[about_Execution_Policies](https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_execution_policies).

## .NET SDK

The version in `global.json` at the root of the repository, or a later
feature band of the same major and minor (10.0.401 accepts 10.0.402 and
10.0.500, not 11.0).

```powershell
winget install --id Microsoft.DotNet.SDK.10 -e
```

Or the installer from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0).
`dotnet --list-sdks` shows what you have.

## MAUI workloads

The Windows and Android clients are .NET MAUI; the solution builds them, so
the `maui-windows` and `android` workloads are needed even to build the agent
alone. From the root of the repository:

```powershell
dotnet workload restore WslcAgent.slnx
```

It installs exactly the workloads the solution uses. `dotnet workload list`
shows what you have. After a .NET SDK update, run it again: workloads belong
to an SDK band. More in
[Install .NET MAUI](https://learn.microsoft.com/dotnet/maui/get-started/installation?tabs=visual-studio-code).

## NuGet access

The first build downloads every package from nuget.org, the WiX Toolset
included (the installers use it as an MSBuild SDK, so there is nothing to
install by hand). Behind a proxy, configure it for `dotnet`
([NuGet proxy settings](https://learn.microsoft.com/nuget/reference/nuget-config-file#config-section)).

## WSLC

**WSLC 3.0.1 or later**, the first generally available release: install the
[latest WSL release](https://github.com/microsoft/WSL/releases/latest), then open a new terminal; `wslc version`
prints the one you have. The agent works with 2.9.13 or later; an older
`wslc` is reported as `[broken]`.

Without it everything builds and the tests pass (they do not call `wslc`),
and the agent runs with no containers to manage.

## JDK

The Android build and `keytool`, which makes the signing key, need a JDK.
.NET MAUI uses Microsoft's build of OpenJDK 17:

```powershell
winget install --id Microsoft.OpenJDK.17 -e
```

Or from [Microsoft Build of OpenJDK](https://learn.microsoft.com/java/openjdk/download).
If it lives somewhere the scripts do not look, set `JAVA_HOME` to its folder
(the one that contains `bin\keytool.exe`).

## Android SDK

**With VS Code**, or with no IDE: the `android` workload installs the SDK
itself. From the root of the repository, with the JDK above installed:

```powershell
dotnet build src\WslcAgent.App\WslcAgent.App.csproj -t:InstallAndroidDependencies -f net10.0-android "-p:AndroidSdkDirectory=$env:LOCALAPPDATA\Android\Sdk" "-p:AcceptAndroidSDKLicenses=True"
```

It downloads the platform, build tools and platform tools the project needs
into `%LOCALAPPDATA%\Android\Sdk`. Running it means accepting the Android SDK
licences.

**With Android Studio**: its SDK Manager installs the SDK in the same
folder, and its Device Manager creates emulators, which `debug-android.ps1`
starts.

**With Visual Studio**: its .NET MAUI workload installs the SDK under
`C:\Program Files (x86)\Android\android-sdk`.

The scripts find it in any of those folders; anywhere else, set
`ANDROID_HOME` to the folder that contains `platform-tools\adb.exe`.

To run the client on an emulator you also need one (an AVD): Android Studio's
Device Manager is the simplest way. A phone with USB debugging on works
without any: `.\debug-android.ps1 -Phone`. More in
[Set up Android devices for .NET MAUI](https://learn.microsoft.com/dotnet/maui/android/emulator/device-manager).

## WebView2 Runtime

The Windows client and the agent's tray window show their pages in WebView2.
Windows 11 ships it; if it was removed:

```powershell
winget install --id Microsoft.EdgeWebView2Runtime -e
```

Or the Evergreen installer from
[developer.microsoft.com](https://developer.microsoft.com/microsoft-edge/webview2/).

## Editor: VS Code

Any editor works: every step runs from the scripts. For VS Code, install:

- [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit):
  the solution explorer, IntelliSense, tests and debugging.
- [.NET MAUI](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.dotnet-maui):
  building, deploying and debugging the Windows and Android clients.

C# Dev Kit from a terminal:

```powershell
code --install-extension ms-dotnettools.csdevkit
```

.NET MAUI from a terminal:

```powershell
code --install-extension ms-dotnettools.dotnet-maui
```

Open the repository folder; C# Dev Kit finds `WslcAgent.slnx`. The
[.NET MAUI in VS Code guide](https://learn.microsoft.com/dotnet/maui/get-started/installation?tabs=visual-studio-code)
covers the extension's own settings, including where it looks for the
Android SDK and the JDK.

Visual Studio 2022 or later, with the .NET MAUI workload, works as well and
installs the JDK and the Android SDK itself.
