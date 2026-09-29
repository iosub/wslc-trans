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
# The oldest WSLC the agent works with.
$MinimumWslc = [version]"2.9.13"
# Where each prerequisite is explained and installed, section by section.
$Guide = "docs\developer\prerequisites.md"

function Get-CommandVersion([string]$Name, [string[]]$Arguments) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) { return $null }
    try { return ((& $Name @Arguments 2>$null) | Select-Object -First 1) } catch { return $null }
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
else { Write-WslcAgentCheck broken "git" "not found. Install it: winget install --id Git.Git -e" -Guide "$Guide#git" }
Write-Host ""

# --- .NET ------------------------------------------------------------------
Write-Host ".NET" -ForegroundColor Cyan
$wanted = [version](Get-Content -LiteralPath (Join-Path $RepoRoot "global.json") -Raw | ConvertFrom-Json).sdk.version
$sdks = @()
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    $sdks = @(& dotnet --list-sdks 2>$null | ForEach-Object { [version](($_ -split " ")[0] -replace "-.*$", "") })
}
# global.json rolls forward to the latest feature band and patch of the same
# major.minor, never to another major.minor.
$usable = @($sdks | Where-Object { $_.Major -eq $wanted.Major -and $_.Minor -eq $wanted.Minor -and $_.Build -ge $wanted.Build } | Sort-Object -Descending)
$dotnetOk = $usable.Count -gt 0
if ($dotnetOk) {
    Write-WslcAgentCheck ok ".NET SDK" "$($usable[0]) (global.json asks for $wanted or a later feature band)."
} elseif ($sdks.Count -gt 0) {
    Write-WslcAgentCheck broken ".NET SDK" "found $($sdks -join ', '); global.json asks for $wanted or later in $($wanted.Major).$($wanted.Minor). Install it: winget install --id Microsoft.DotNet.SDK.$($wanted.Major) -e" -Guide "$Guide#net-sdk"
} else {
    Write-WslcAgentCheck broken ".NET SDK" "dotnet not found. Install it: winget install --id Microsoft.DotNet.SDK.$($wanted.Major) -e" -Guide "$Guide#net-sdk"
}

if ($dotnetOk) {
    $installed = @(& dotnet workload list 2>$null | ForEach-Object { ($_.Trim() -split "\s+")[0] })
    foreach ($workload in @("maui-windows", "android")) {
        if ($installed -contains $workload) {
            Write-WslcAgentCheck ok "workload $workload" "installed."
        } else {
            Write-WslcAgentCheck broken "workload $workload" "missing; the client project needs it. Install every workload the solution uses: dotnet workload restore WslcAgent.slnx" -Guide "$Guide#maui-workloads"
        }
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
$androidSdk = Find-WslcAgentAndroidSdk
if ($androidSdk) {
    Write-WslcAgentCheck ok "Android SDK" $androidSdk
} else {
    Write-WslcAgentCheck absent "Android SDK" "no APK, no debug-android.ps1. Install it with the android workload (VS Code, no IDE), Android Studio or Visual Studio; if it lives elsewhere, set ANDROID_HOME." -Guide "$Guide#android-sdk"
}
$keytool = try { Find-WslcAgentKeytool } catch { $null }
if ($keytool) {
    Write-WslcAgentCheck ok "JDK (keytool)" $keytool
} else {
    Write-WslcAgentCheck absent "JDK (keytool)" "no Android build nor signing key. Install it: winget install --id Microsoft.OpenJDK.17 -e, or set JAVA_HOME." -Guide "$Guide#jdk"
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
    Write-WslcAgentCheck absent "WebView2 Runtime" "the Windows client and the tray window need it to run. Install it: winget install --id Microsoft.EdgeWebView2Runtime -e" -Guide "$Guide#webview2-runtime"
}
Write-Host ""

# --- WSLC ----------------------------------------------------------------------
Write-Host "WSLC" -ForegroundColor Cyan
$wslcRelease = "https://github.com/microsoft/WSL/releases/tag/$MinimumWslc"
$wslc = Get-CommandVersion wslc @("version")
$wslcVersion = if ($wslc -match '(\d+\.\d+\.\d+(\.\d+)?)') { [version]$Matches[1] } else { $null }
if (-not $wslc) {
    Write-WslcAgentCheck absent "wslc" "not on the PATH: everything builds and the tests pass, and the agent has no containers to manage. Install WSLC $MinimumWslc or later ($wslcRelease), then open a new terminal." -Guide "$Guide#wslc"
} elseif (-not $wslcVersion) {
    Write-WslcAgentCheck broken "wslc" "answers '$wslc', with no version in it; the agent needs $MinimumWslc or later ($wslcRelease)." -Guide "$Guide#wslc"
} elseif ($wslcVersion -lt $MinimumWslc) {
    Write-WslcAgentCheck broken "wslc" "$wslcVersion is older than $MinimumWslc, the oldest the agent works with. Update it: $wslcRelease" -Guide "$Guide#wslc"
} else {
    Write-WslcAgentCheck ok "wslc" "$wslcVersion (the agent needs $MinimumWslc or later)."
}
Write-Host ""

if ($script:WslcAgentCheckBroken -gt 0) {
    Write-Host "$($script:WslcAgentCheckBroken) problem(s) above. Fix them and run this again." -ForegroundColor Red
    exit 1
}
Write-Host "Ready to build: .\build.ps1. What is absent only switches off what it names." -ForegroundColor Green
