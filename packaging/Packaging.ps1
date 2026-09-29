<#
.SYNOPSIS
    Helpers shared by the build-*.ps1 packaging scripts. Dot-source it.
.DESCRIPTION
    Version bumps (agent: Directory.Build.props; client: the MAUI csproj),
    WiX file-list generation from a published payload, MSI Explorer version
    stamping, third-party notices, and Android signing key resolution.
#>

Set-StrictMode -Version Latest

function Get-WslcAgentRepoRoot {
    return (Split-Path -Parent $PSScriptRoot)
}

# ---------------------------------------------------------------- versions --

function Get-NextPatchVersion {
    param([Parameter(Mandatory = $true)][string]$Version)
    $parts = @()
    foreach ($piece in ($Version.Trim() -split '\.')) {
        if ($piece -ne "") { $parts += [int]$piece }
    }
    while ($parts.Count -lt 3) { $parts += 0 }
    $parts[2] = [int]$parts[2] + 1
    return ($parts[0..2] -join '.')
}

function ConvertTo-WixProductVersion {
    # MSI ProductVersion is major.minor.build, each part numeric.
    param([Parameter(Mandatory = $true)][string]$Display)
    $parts = @()
    foreach ($piece in ($Display.Trim() -split '\.')) {
        if ($piece -ne "") { $parts += $piece }
    }
    while ($parts.Count -lt 3) { $parts += "0" }
    return ($parts[0..2] -join '.')
}

function Update-WslcAgentVersion {
    <# Agent version: the single <Version> in Directory.Build.props. #>
    param([switch]$NoBump)
    $file = Join-Path (Get-WslcAgentRepoRoot) "Directory.Build.props"
    $text = [System.IO.File]::ReadAllText($file)
    if ($text -notmatch '<Version>([^<]+)</Version>') {
        throw "No <Version> element in $file"
    }
    $current = $Matches[1].Trim()
    if ($NoBump) { return $current }
    $next = Get-NextPatchVersion $current
    $text = [regex]::Replace($text, '<Version>[^<]+</Version>', "<Version>$next</Version>", 1)
    [System.IO.File]::WriteAllText($file, $text, (New-Object System.Text.UTF8Encoding $false))
    Write-Host "Bumped agent version $current -> $next" -ForegroundColor Cyan
    return $next
}

function Update-WslcAgentClientVersion {
    <# Client version: ApplicationDisplayVersion (patch) and ApplicationVersion (+1) in the MAUI csproj. #>
    param(
        [Parameter(Mandatory = $true)][string]$Csproj,
        [switch]$NoBump
    )
    $text = [System.IO.File]::ReadAllText($Csproj)
    if ($text -notmatch '<ApplicationDisplayVersion>([^<]+)</ApplicationDisplayVersion>') {
        throw "No <ApplicationDisplayVersion> in $Csproj"
    }
    $display = $Matches[1].Trim()
    if ($text -notmatch '<ApplicationVersion>([^<]+)</ApplicationVersion>') {
        throw "No <ApplicationVersion> in $Csproj"
    }
    $build = [int]$Matches[1].Trim()
    if ($NoBump) {
        return [pscustomobject]@{ Display = $display; Build = $build }
    }
    $nextDisplay = Get-NextPatchVersion $display
    $nextBuild = $build + 1
    $text = [regex]::Replace($text, '<ApplicationDisplayVersion>[^<]+</ApplicationDisplayVersion>', "<ApplicationDisplayVersion>$nextDisplay</ApplicationDisplayVersion>", 1)
    $text = [regex]::Replace($text, '<ApplicationVersion>[^<]+</ApplicationVersion>', "<ApplicationVersion>$nextBuild</ApplicationVersion>", 1)
    [System.IO.File]::WriteAllText($Csproj, $text, (New-Object System.Text.UTF8Encoding $false))
    Write-Host "Bumped client version $display ($build) -> $nextDisplay ($nextBuild)" -ForegroundColor Cyan
    return [pscustomobject]@{ Display = $nextDisplay; Build = $nextBuild }
}

# --------------------------------------------------------------- payloads --

function Copy-WslcAgentNotices {
    <# LICENSE and the third-party notices ship with every binary. #>
    param([Parameter(Mandatory = $true)][string]$Destination)
    $root = Get-WslcAgentRepoRoot
    $licenses = Join-Path $Destination "licenses"
    New-Item -ItemType Directory -Force -Path $licenses | Out-Null
    Copy-Item -LiteralPath (Join-Path $root "LICENSE") -Destination (Join-Path $Destination "LICENSE.txt") -Force
    Copy-Item -LiteralPath (Join-Path $root "THIRD-PARTY-NOTICES.md") -Destination $Destination -Force
    Copy-Item -LiteralPath (Join-Path $root "packaging\licenses\Apache-2.0.txt") -Destination $licenses -Force
}

function Clear-WslcAgentIconCache {
    <#
    MAUI's resizetizer caches generated icons and splash images under
    obj\**\resizetizer and does not always notice a change in the MauiIcon
    metadata (e.g. removing Color left opaque corners in the Windows .ico).
    Regenerating costs seconds, so every client build starts clean.
    #>
    $appObj = Join-Path (Get-WslcAgentRepoRoot) "src\WslcAgent.App\obj"
    Get-ChildItem -Path $appObj -Directory -Recurse -Filter resizetizer -ErrorAction SilentlyContinue |
        ForEach-Object { Remove-Item -Recurse -Force -LiteralPath $_.FullName }
}

function Remove-WslcAgentDebugFiles {
    param([Parameter(Mandatory = $true)][string]$Path)
    Get-ChildItem -LiteralPath $Path -Recurse -Filter *.pdb -ErrorAction SilentlyContinue |
        ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
}

# -------------------------------------------------------------------- WiX --

function ConvertTo-WixId([string]$Prefix, [string]$Value) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Value))
    } finally {
        $sha.Dispose()
    }
    return $Prefix + (-join ($bytes[0..9] | ForEach-Object { $_.ToString("x2") }))
}

function ConvertTo-XmlText([string]$Value) {
    return ($Value -replace "&", "&amp;" -replace "<", "&lt;" -replace ">", "&gt;" -replace '"', "&quot;")
}

function Write-WslcAgentWixFileList {
    <# Emit a WiX fragment that installs every file under $Stage into INSTALLFOLDER. #>
    param(
        [Parameter(Mandatory = $true)][string]$Stage,
        [Parameter(Mandatory = $true)][string]$OutFile,
        [string]$ComponentGroupId = "PublishedFiles"
    )
    $stageFull = (Resolve-Path -LiteralPath $Stage).Path.TrimEnd('\')
    $files = @(Get-ChildItem -LiteralPath $stageFull -Recurse -File)
    if ($files.Count -eq 0) { throw "No files to package under $stageFull" }

    $dirIds = @{ "" = "INSTALLFOLDER" }
    $relDirs = New-Object "System.Collections.Generic.HashSet[string]"
    foreach ($file in $files) {
        $rel = $file.FullName.Substring($stageFull.Length).TrimStart('\', '/')
        $dirRel = Split-Path -Parent $rel
        if (-not $dirRel) { continue }
        $acc = ""
        foreach ($part in ($dirRel -split '[\\/]')) {
            $acc = if ($acc) { "$acc\$part" } else { $part }
            [void]$relDirs.Add($acc)
            if (-not $dirIds.ContainsKey($acc)) { $dirIds[$acc] = ConvertTo-WixId "d" $acc }
        }
    }
    $children = @{}
    foreach ($rel in $relDirs) {
        $parent = Split-Path -Parent $rel
        if (-not $parent) { $parent = "" }
        if (-not $children.ContainsKey($parent)) { $children[$parent] = New-Object System.Collections.Generic.List[string] }
        $children[$parent].Add($rel)
    }
    $sb = New-Object System.Text.StringBuilder
    function Write-DirTree([string]$parentRel, [int]$indent) {
        if (-not $children.ContainsKey($parentRel)) { return }
        $pad = " " * $indent
        foreach ($rel in ($children[$parentRel] | Sort-Object)) {
            [void]$sb.AppendLine("$pad<Directory Id=`"$($dirIds[$rel])`" Name=`"$(ConvertTo-XmlText (Split-Path -Leaf $rel))`">")
            Write-DirTree $rel ($indent + 2)
            [void]$sb.AppendLine("$pad</Directory>")
        }
    }
    [void]$sb.AppendLine('<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">')
    [void]$sb.AppendLine('  <!-- Generated by packaging\Packaging.ps1 from the published payload. Do not edit. -->')
    [void]$sb.AppendLine('  <Fragment>')
    [void]$sb.AppendLine('    <DirectoryRef Id="INSTALLFOLDER">')
    Write-DirTree "" 6
    [void]$sb.AppendLine('    </DirectoryRef>')
    [void]$sb.AppendLine("    <ComponentGroup Id=`"$ComponentGroupId`">")
    foreach ($file in ($files | Sort-Object FullName)) {
        $rel = $file.FullName.Substring($stageFull.Length).TrimStart('\', '/')
        $dirRel = Split-Path -Parent $rel
        if (-not $dirRel) { $dirRel = "" }
        # DefaultLanguage only matters for versioned files without a language
        # resource; on unversioned files WiX warns that it is ignored (WIX1102).
        # The extension does not say which is which — the WinAppSDK ships native
        # DLLs with no version resource at all (marshal.dll,
        # Microsoft.UI.Composition.OSSupport.dll, Microsoft.UI.Windowing.dll) —
        # so the file itself is asked.
        $lang = if (Test-WslcAgentVersionedFile $file.FullName) { ' DefaultLanguage="0"' } else { "" }
        [void]$sb.AppendLine("      <Component Id=`"$(ConvertTo-WixId 'c' $rel)`" Directory=`"$($dirIds[$dirRel])`" Guid=`"*`">")
        [void]$sb.AppendLine("        <File Id=`"$(ConvertTo-WixId 'f' $rel)`" Source=`"$(ConvertTo-XmlText $file.FullName)`" KeyPath=`"yes`"$lang />")
        [void]$sb.AppendLine("      </Component>")
    }
    [void]$sb.AppendLine('    </ComponentGroup>')
    [void]$sb.AppendLine('  </Fragment>')
    [void]$sb.AppendLine('</Wix>')
    [System.IO.File]::WriteAllText($OutFile, $sb.ToString(), (New-Object System.Text.UTF8Encoding $false))
    Write-Host "Wrote $OutFile ($($files.Count) files)" -ForegroundColor DarkGray
}

function Test-WslcAgentVersionedFile {
    <# True when the file carries a version resource, which is what makes MSI
       treat it as versioned and DefaultLanguage meaningful (WIX1102). #>
    param([Parameter(Mandatory = $true)][string]$Path)
    try {
        $info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($Path)
        return -not [string]::IsNullOrEmpty($info.FileVersion)
    }
    catch {
        # Not a binary Windows can read a version out of: unversioned, then.
        return $false
    }
}

function Build-WslcAgentMsi {
    <# Build a WiX project and return the path of the produced .msi. #>
    param(
        [Parameter(Mandatory = $true)][string]$WixProj,
        [Parameter(Mandatory = $true)][string]$Version
    )
    $outDir = Join-Path (Split-Path -Parent $WixProj) "bin\Release"
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
    # Out-Host keeps the build output off the pipeline: this function's only
    # return value must be the .msi path.
    & dotnet build $WixProj -c Release -nologo -v q -p:OutputPath="$outDir\" -p:MsiVersion=$Version | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "WiX build failed with exit code $LASTEXITCODE" }
    $msi = Get-ChildItem -LiteralPath $outDir -Filter *.msi -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $msi) { throw "WiX build succeeded but no .msi was found under $outDir" }
    return $msi.FullName
}

function Set-WslcAgentMsiExplorerVersion {
    <# Stamp the MSI Summary Information so Explorer's Details tab shows the version. #>
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Version
    )
    $label = "$Name $Version"
    $full = (Resolve-Path -LiteralPath $Path).Path
    $installer = New-Object -ComObject WindowsInstaller.Installer
    $sum = $null
    try {
        $sum = $installer.GetType().InvokeMember("SummaryInformation", [Reflection.BindingFlags]::GetProperty, $null, $installer, @($full, 20))
        foreach ($id in @(2, 3, 6, 18)) {
            $value = if ($id -eq 6) { $Version } else { $label }
            [void]$sum.GetType().InvokeMember("Property", [Reflection.BindingFlags]::SetProperty, $null, $sum, @([int]$id, [string]$value))
        }
        [void]$sum.GetType().InvokeMember("Persist", [Reflection.BindingFlags]::InvokeMethod, $null, $sum, @())
    } finally {
        if ($null -ne $sum) { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($sum) }
        [void][Runtime.InteropServices.Marshal]::ReleaseComObject($installer)
        [GC]::Collect()
        [GC]::WaitForPendingFinalizers()
    }
}

# ---------------------------------------------------------------- Android --

function Get-WslcAgentPrivateFolder {
    <#
    The checkout's private/ folder: the signing key, the Firebase files and
    env.psd1. Git tracks nothing in it but README.md and env.example.psd1
    (.gitignore); docs/developer/private-files.md says how to make each file.
    #>
    return Join-Path (Split-Path -Parent $PSScriptRoot) "private"
}

function Import-WslcAgentPrivateSettings {
    <#
    Loads private\env.psd1 into the environment, so a developer fills one file
    instead of setting variables by hand. A variable already set in the
    environment wins over the file, and an empty value in the file sets
    nothing. The file is a PowerShell data file: it is read, never run.
    #>
    $file = Join-Path (Get-WslcAgentPrivateFolder) "env.psd1"
    if (-not (Test-Path -LiteralPath $file)) { return }
    $settings = Import-PowerShellDataFile -LiteralPath $file
    foreach ($name in $settings.Keys) {
        $value = [string]$settings[$name]
        if ($value -and -not [Environment]::GetEnvironmentVariable($name)) {
            [Environment]::SetEnvironmentVariable($name, $value)
        }
    }
}

function Resolve-WslcAgentAndroidSigning {
    <#
    Android refuses to update an app whose new APK is signed with a different
    key, so every machine that builds one has to sign with the same key.
    Resolution order:
      1. WSLC_AGENT_KEYSTORE (+ WSLC_AGENT_KEYSTORE_PASS, WSLC_AGENT_KEY_ALIAS, WSLC_AGENT_KEY_PASS)
      2. private\android.keystore in this checkout, with its password in
         android.keystore.pass beside it
      3. %USERPROFILE%\.wslc-agent\android.keystore, the same pair of files,
         for a machine that keeps one key for several checkouts
    When none of them exists a new keystore is generated at (2). Back it up:
    an APK signed with any other key cannot update the installed app.
    #>
    $keystore = $env:WSLC_AGENT_KEYSTORE
    $storePass = $env:WSLC_AGENT_KEYSTORE_PASS
    $alias = if ($env:WSLC_AGENT_KEY_ALIAS) { $env:WSLC_AGENT_KEY_ALIAS } else { "wslc-agent" }
    $keyPass = $env:WSLC_AGENT_KEY_PASS

    if (-not $keystore) {
        $private = Join-Path (Get-WslcAgentPrivateFolder) "android.keystore"
        $perUser = Join-Path $env:USERPROFILE ".wslc-agent\android.keystore"
        $keystore = if (-not (Test-Path -LiteralPath $private) -and (Test-Path -LiteralPath $perUser)) { $perUser } else { $private }
        $passFile = "$keystore.pass"
        if (-not (Test-Path -LiteralPath $keystore)) {
            $keytool = Find-WslcAgentKeytool
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $keystore) | Out-Null
            $storePass = -join ((1..32) | ForEach-Object { [char](Get-Random -InputObject ([int[]](48..57 + 65..90 + 97..122))) })
            [System.IO.File]::WriteAllText($passFile, $storePass, (New-Object System.Text.UTF8Encoding $false))
            Write-Host "No Android signing key found; generating $keystore" -ForegroundColor Yellow
            & $keytool -genkeypair -v -keystore $keystore -alias $alias -keyalg RSA -keysize 2048 -validity 10000 `
                -storepass $storePass -keypass $storePass -dname "CN=wslc-agent" 2>&1 | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "keytool failed with exit code $LASTEXITCODE" }
            Write-Host "Back up $keystore and its .pass file: every later APK has to be signed with this key to update the installed app." -ForegroundColor Yellow
        }
        if (-not $storePass) {
            if (-not (Test-Path -LiteralPath $passFile)) { throw "Keystore $keystore exists but $passFile is missing; set WSLC_AGENT_KEYSTORE_PASS." }
            $storePass = ([System.IO.File]::ReadAllText($passFile)).Trim()
        }
    } elseif (-not (Test-Path -LiteralPath $keystore)) {
        throw "WSLC_AGENT_KEYSTORE points to a missing file: $keystore"
    } elseif (-not $storePass) {
        throw "WSLC_AGENT_KEYSTORE is set; set WSLC_AGENT_KEYSTORE_PASS too."
    }
    if (-not $keyPass) { $keyPass = $storePass }
    return [pscustomobject]@{ Keystore = $keystore; StorePass = $storePass; Alias = $alias; KeyPass = $keyPass }
}

function Find-WslcAgentKeytool {
    $candidates = @()
    if ($env:JAVA_HOME) { $candidates += (Join-Path $env:JAVA_HOME "bin\keytool.exe") }
    $candidates += Get-ChildItem -Path "${env:ProgramFiles(x86)}\Android\openjdk", "$env:ProgramFiles\Microsoft\jdk-*", "$env:ProgramFiles\Android\jdk" -Filter keytool.exe -Recurse -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty FullName
    $cmd = Get-Command keytool -ErrorAction SilentlyContinue
    if ($cmd) { $candidates += $cmd.Source }
    foreach ($c in $candidates) {
        if ($c -and (Test-Path -LiteralPath $c)) { return $c }
    }
    throw "keytool.exe not found. Install a JDK (the Android workload's OpenJDK is enough) or set JAVA_HOME."
}
