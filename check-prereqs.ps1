<#
.SYNOPSIS
    Check that this machine has what the repository needs to build, test, run
    and package, and say how to install what is missing.
.DESCRIPTION
    Changes nothing on the machine. What every build needs (the .NET SDK
    global.json asks for, its MAUI workloads, NuGet) is required: its absence
    is an error and the script exits 1. What only one part needs (the Android
    SDK and a JDK for the APK, WebView2 for the Windows client, wslc to run the
    agent against containers) is reported as absent with what it switches off;
    a wslc older than the agent works with is an error.
    The private files (signing key, Firebase) are checked by check-private.ps1.
#>
$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $RepoRoot "packaging\Packaging.ps1")
$script:WslcAgentCheckBroken = 0
# The oldest WSLC the agent works with, and the first generally available one.
$MinimumWslc = [version]"2.9.13"
$RecommendedWslc = [version]"3.0.1"
# Where each prerequisite is explained and installed, section by section.
$Guide = "docs\developer\prerequisites.md"

function Get-CommandVersion([string]$Name, [string[]]$Arguments) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) { return $null }
    try { return ((Invoke-WslcAgentNative $Name $Arguments) | Select-Object -First 1) } catch { return $null }
}

Write-Host "Prerequisites of $RepoRoot" -ForegroundColor Cyan
Write-Host ""

# --- Windows and git -------------------------------------------------------
Write-Host "Machine" -ForegroundColor Cyan
$build = [Environment]::OSVersion.Version.Build
if ($build -ge 22000) {
    Write-WslcAgentCheck ok "Windows" "build $build."
} elseif ($build -ge 17763) {
    Write-WslcAgentCheck absent "Windows 11" "build $build is Windows 10: everything builds, and WSLC needs Windows 11 to run the agent against containers." -Guide "$Guide#windows"
} else {
    Write-WslcAgentCheck broken "Windows" "build $build is older than 10.0.17763, the oldest the Windows client supports." -Guide "$Guide#windows"
}

$git = Get-CommandVersion git @("--version")
if ($git) { Write-WslcAgentCheck ok "git" $git }
else { Write-WslcAgentCheck broken "git" "not found." -Run "winget install --id Git.Git -e" -Guide "$Guide#git" }
Write-Host ""

# --- .NET ------------------------------------------------------------------
Write-Host ".NET" -ForegroundColor Cyan
$wanted = [version](Get-Content -LiteralPath (Join-Path $RepoRoot "global.json") -Raw | ConvertFrom-Json).sdk.version
$sdks = @()
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    $sdks = @(Invoke-WslcAgentNative dotnet @("--list-sdks") | ForEach-Object { [version](($_ -split " ")[0] -replace "-.*$", "") })
}
# global.json rolls forward to the latest feature band and patch of the same
# major.minor, never to another major.minor.
$usable = @($sdks | Where-Object { $_.Major -eq $wanted.Major -and $_.Minor -eq $wanted.Minor -and $_.Build -ge $wanted.Build } | Sort-Object -Descending)
$dotnetOk = $usable.Count -gt 0
if ($dotnetOk) {
    Write-WslcAgentCheck ok ".NET SDK" "$($usable[0]) (global.json asks for $wanted or a later feature band)."
} elseif ($sdks.Count -gt 0) {
    Write-WslcAgentCheck broken ".NET SDK" "found $($sdks -join ', '); global.json asks for $wanted or later in $($wanted.Major).$($wanted.Minor)." -Run "winget install --id Microsoft.DotNet.SDK.$($wanted.Major) -e" -Guide "$Guide#net-sdk"
} else {
    Write-WslcAgentCheck broken ".NET SDK" "dotnet not found; once installed, open a new terminal and run this check again for its workloads." -Run "winget install --id Microsoft.DotNet.SDK.$($wanted.Major) -e" -Guide "$Guide#net-sdk"
}

if ($dotnetOk) {
    # A workload brings the ones it extends: maui-android brings android and
    # maui-blazor, and dotnet workload restore may pick maui-tizen for the
    # Windows target. So what counts is what the installed ones bring, read
    # from the SDK's own workload manifests, not their names.
    $installed = @(Invoke-WslcAgentNative dotnet @("workload", "list") | ForEach-Object { ($_.Trim() -split "\s+")[0] })
    $extends = @{}
    $manifestRoots = @((Split-Path -Parent (Get-Command dotnet).Source), (Join-Path $env:USERPROFILE ".dotnet")) | ForEach-Object { Join-Path $_ "sdk-manifests" }
    Get-ChildItem -LiteralPath @($manifestRoots | Where-Object { Test-Path -LiteralPath $_ }) -Directory -Filter "$($wanted.Major).$($wanted.Minor).*" -ErrorAction SilentlyContinue |
        Get-ChildItem -Recurse -Filter "WorkloadManifest.json" | ForEach-Object {
            try { $manifest = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json } catch { return }
            foreach ($workload in @($manifest.workloads.PSObject.Properties)) {
                $bases = $workload.Value.PSObject.Properties["extends"]
                if ($bases) { $extends[$workload.Name] = @($extends[$workload.Name]) + @($bases.Value) | Where-Object { $_ } | Select-Object -Unique }
            }
        }
    function Get-WorkloadsBroughtBy([string]$Workload, [hashtable]$Seen = @{}) {
        if ($Seen.ContainsKey($Workload)) { return }
        $Seen[$Workload] = $true
        $Workload
        foreach ($base in @($extends[$Workload])) { if ($base) { Get-WorkloadsBroughtBy $base $Seen } }
    }
    # android for the Android client; maui-blazor, the MAUI core with the
    # Blazor web view, for the Windows client (maui-windows brings it).
    $missing = @()
    foreach ($needed in @("android", "maui-blazor")) {
        $by = @($installed | Where-Object { $_ -and (@(Get-WorkloadsBroughtBy $_) -contains $needed) })
        if ($by.Count -gt 0) {
            Write-WslcAgentCheck ok "workload $needed" "installed$(if ($by -notcontains $needed) { ", brought by $($by -join ', ')" })."
        } else {
            $missing += $needed
        }
    }
    # One command installs every missing one: a line for all of them. It
    # names the workloads of the project's two targets, maui-android bringing
    # android, rather than leaving the choice to dotnet workload restore.
    if ($missing.Count -gt 0) {
        Write-WslcAgentCheck broken "workload $($missing -join ', ')" "missing; the client project needs it." -Run "dotnet workload install maui-windows maui-android" -Admin -Guide "$Guide#maui-workloads"
    }
}

try {
    Invoke-WebRequest -Uri "https://api.nuget.org/v3/index.json" -Method Head -TimeoutSec 15 -UseBasicParsing | Out-Null
    Write-WslcAgentCheck ok "nuget.org" "reachable: the packages, WiX included, are restored on the first build."
} catch {
    Write-WslcAgentCheck broken "nuget.org" "not reachable: the first build downloads every package, WiX included, from it. Check the network or proxy." -Guide "$Guide#nuget-access"
}
Write-Host ""

# --- Android -----------------------------------------------------------------
Write-Host "Android client" -ForegroundColor Cyan
# The JDK first: installing the Android SDK needs it.
$keytool = try { Find-WslcAgentKeytool } catch { $null }
if ($keytool) {
    Write-WslcAgentCheck ok "JDK (keytool)" $keytool
} else {
    Write-WslcAgentCheck absent "JDK (keytool)" "build.ps1 builds without the Android client; no APK, no signing key. Installed elsewhere? Set JAVA_HOME." -Run "winget install --id Microsoft.OpenJDK.17 -e" -Guide "$Guide#jdk"
}
$androidSdk = Find-WslcAgentAndroidSdk
if ($androidSdk) {
    Write-WslcAgentCheck ok "Android SDK" $androidSdk
} else {
    # The android workload downloads what the project needs; running it
    # accepts the Android SDK licences.
    $app = Join-Path $RepoRoot "src\WslcAgent.App\WslcAgent.App.csproj"
    $sdkFolder = Join-Path $env:LOCALAPPDATA "Android\Sdk"
    Write-WslcAgentCheck absent "Android SDK" "build.ps1 builds without the Android client; no APK, no debug-android.ps1. The command needs the JDK and the workloads above, and accepts the Android SDK licences; Android Studio or Visual Studio install it too. Installed elsewhere? Set ANDROID_HOME." -Run "dotnet build `"$app`" -t:InstallAndroidDependencies -f net10.0-android `"-p:AndroidSdkDirectory=$sdkFolder`" `"-p:AcceptAndroidSDKLicenses=True`"" -Guide "$Guide#android-sdk"
}
Write-Host ""

# --- Windows client and tray -------------------------------------------------
Write-Host "Windows client and tray" -ForegroundColor Cyan
$webView2Key = "Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"
$webView2 = @("HKLM:\SOFTWARE\WOW6432Node\$webView2Key", "HKLM:\SOFTWARE\$webView2Key", "HKCU:\SOFTWARE\$webView2Key") |
    ForEach-Object { Get-ItemProperty -Path $_ -Name pv -ErrorAction SilentlyContinue } |
    Where-Object { $_.pv -and $_.pv -ne "0.0.0.0" } | Select-Object -First 1
if ($webView2) {
    Write-WslcAgentCheck ok "WebView2 Runtime" $webView2.pv
} else {
    Write-WslcAgentCheck absent "WebView2 Runtime" "the Windows client and the tray window need it to run." -Run "winget install --id Microsoft.EdgeWebView2Runtime -e" -Guide "$Guide#webview2-runtime"
}
Write-Host ""

# --- WSL and WSLC --------------------------------------------------------------
Write-Host "WSL and WSLC" -ForegroundColor Cyan
# WSLC is part of WSL. Windows ships a wsl.exe of its own that only offers to
# install WSL: --version answers only once WSL itself is installed. wsl.exe
# writes UTF-16 unless WSL_UTF8 asks for UTF-8.
$previousUtf8 = $env:WSL_UTF8
$env:WSL_UTF8 = "1"
$wslLine = Get-CommandVersion wsl @("--version")
$env:WSL_UTF8 = $previousUtf8
$wslVersion = if ($wslLine -match '(\d+\.\d+\.\d+(\.\d+)?)') { [version]$Matches[1] } else { $null }
if (-not $wslVersion) {
    Write-WslcAgentCheck absent "WSL" "not installed: the agent has no containers to manage. Restart Windows after installing it." -Run "wsl --install --no-distribution" -Admin -Guide "$Guide#wsl"
} elseif ($wslVersion -lt $MinimumWslc) {
    Write-WslcAgentCheck broken "WSL" "$wslVersion is older than $MinimumWslc, the first with WSLC." -Run "wsl --update" -Guide "$Guide#wsl"
} else {
    Write-WslcAgentCheck ok "WSL" "$wslVersion."
}

# WSL starts its virtual machine through the Host Compute Service, which the
# Virtual Machine Platform feature installs; without it wslc fails with
# HCS_E_SERVICE_NOT_AVAILABLE. Whether the hypervisor is present says nothing
# here: inside a virtual machine Windows always reports one.
if (Get-Service vmcompute -ErrorAction SilentlyContinue) {
    Write-WslcAgentCheck ok "Virtual Machine Platform" "the Host Compute Service is installed."
} else {
    Write-WslcAgentCheck absent "Virtual Machine Platform" "not enabled: WSL cannot start its virtual machine (HCS_E_SERVICE_NOT_AVAILABLE), so no container runs. Restart Windows after enabling it; in a virtual machine the host has to expose virtualization to it too." -Run "Enable-WindowsOptionalFeature -Online -FeatureName VirtualMachinePlatform -All" -Admin -Guide "$Guide#wsl"
}

$wslcRelease = "https://github.com/microsoft/WSL/releases/latest"
$wslc = Get-CommandVersion wslc @("version")
$wslcVersion = if ($wslc -match '(\d+\.\d+\.\d+(\.\d+)?)') { [version]$Matches[1] } else { $null }
# WSLC comes with WSL: updating WSL brings it, or the installer of the latest
# release does.
if (-not $wslc) {
    Write-WslcAgentCheck absent "wslc" "not on the PATH: everything builds and the tests pass, and the agent has no containers to manage. Install WSLC $RecommendedWslc or later with WSL (or from $wslcRelease), then open a new terminal." -Run "wsl --update" -Guide "$Guide#wslc"
} elseif (-not $wslcVersion) {
    Write-WslcAgentCheck broken "wslc" "answers '$wslc', with no version in it; the agent needs $MinimumWslc or later ($wslcRelease)." -Run "wsl --update" -Guide "$Guide#wslc"
} elseif ($wslcVersion -lt $MinimumWslc) {
    Write-WslcAgentCheck broken "wslc" "$wslcVersion is older than $MinimumWslc, the oldest the agent works with ($wslcRelease)." -Run "wsl --update" -Guide "$Guide#wslc"
} elseif ($wslcVersion -lt $RecommendedWslc) {
    Write-WslcAgentCheck ok "wslc" "$wslcVersion works; $RecommendedWslc, the first generally available WSLC, is recommended ($wslcRelease)." -Run "wsl --update"
} else {
    Write-WslcAgentCheck ok "wslc" "$wslcVersion (the agent needs $MinimumWslc or later)."
}
Write-Host ""

if ($script:WslcAgentCheckBroken -gt 0) {
    Write-Host "$($script:WslcAgentCheckBroken) problem(s) above. Fix them, close every terminal and VS Code, open them again and run this again." -ForegroundColor Red
    exit 1
}
Write-Host "Ready to build: .\build.ps1. What is absent only switches off what it names." -ForegroundColor Green
