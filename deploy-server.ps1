#Requires -Version 5.1
<#
.SYNOPSIS
  Copy the built dist artifacts to the machine that runs the agent, over SSH,
  and optionally install the agent there.

.DESCRIPTION
  scp copies the artifacts from the local dist folder to the target's dist
  folder file by file, directly or through an SSH jump host (a VPS the target
  keeps a reverse tunnel to: see vps-tunnels.ps1). Once your public key is in
  the target's authorized keys the transfer asks for no password. If a
  destination file is open or locked on the target (the installer running, the
  file open in Explorer), that file fails with "dest open ...: Failure"; the
  script then offers to retry ONLY the failed files after you close them,
  without copying again the ones that succeeded.

  Where to copy comes from the environment, private\env.psd1 included
  (docs\developer\environment.md); a parameter on the command line wins:

    WSLC_DEPLOY_USER   Windows account on the target used for SSH (required)
    WSLC_DEPLOY_HOST   the target as SSH reaches it (required); behind a jump
                       host, the address on the jump host's side, usually
                       127.0.0.1
    WSLC_DEPLOY_PORT   SSH port on that host (default 22)
    WSLC_DEPLOY_JUMP   SSH jump host, user@host (optional)
    WSLC_DEPLOY_DIST   destination folder on the target (required), with
                       forward slashes: C:/wslc/dist

  An account that is a local administrator on the target keeps its public key
  in C:\ProgramData\ssh\administrators_authorized_keys, not in its own profile.

.PARAMETER Files
  Artifacts to copy, relative to the local dist folder.

.NOTES
  Copying and installing are two questions, not one flow. A plain run asks what
  to copy (all, one by one, or none) and then whether to install the MSI that is
  on the target. Answering none to the first and yes to the second installs what
  an earlier run already left there. -Install does the install alone with no
  questions; -All copies everything and asks nothing.

.EXAMPLE
  .\deploy-server.ps1
      Copy wslc-ai-agent.msi, wslc-ai-client.apk and wslc-ai-client.msi to the
      target, then offer to install the agent there.

.EXAMPLE
  .\deploy-server.ps1 -Install
      Install the MSI already on the target, copying nothing.

.EXAMPLE
  .\deploy-server.ps1 -User builder -RemoteDist "D:/wslc/dist"
#>
[CmdletBinding()]
param(
    [string]$User,
    [string]$RemoteHost,
    [string]$Port,
    [string]$JumpHost,
    [string]$RemoteDist,
    [string[]]$Files = @(
        "wslc-ai-agent.msi",
        "wslc-ai-client.apk",
        "wslc-ai-client.msi"
    ),
    # -Install runs the already-deployed wslc-ai-agent.msi on the target
    # silently over SSH (msiexec /qn). It does NOT copy anything from here;
    # deploy the MSI first with a plain run. The MSI is per-user, so over SSH
    # it installs for the SSH account: when the agent runs in another
    # account's desktop session, that agent is not the one updated.
    [switch]$Install,
    [string]$BindHost = "127.0.0.1",
    [int]$AgentPort = 8069,
    [string]$MsiName = "wslc-ai-agent.msi",
    # Copy every artifact without the interactive "all or one-by-one" prompt.
    [switch]$All
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptDir "packaging\Packaging.ps1")
Import-WslcAgentPrivateSettings
$DistDir = Join-Path $ScriptDir "dist"

$User = Get-WslcAgentSetting WSLC_DEPLOY_USER $User -Required -What "the Windows account on the target used for SSH"
$RemoteHost = Get-WslcAgentSetting WSLC_DEPLOY_HOST $RemoteHost -Required -What "the target as SSH reaches it (behind a jump host, usually 127.0.0.1)"
$Port = Get-WslcAgentSetting WSLC_DEPLOY_PORT $Port -Default "22"
$JumpHost = Get-WslcAgentSetting WSLC_DEPLOY_JUMP $JumpHost
# Forward slashes only. scp writes them fine on Windows, but a backslash is an
# escape character in SFTP, so "C:\wslc/..." reaches the target as "C:wslc/..."
# — a path relative to the SSH session's folder — and every file fails to open
# at the destination, which reads exactly like the file being locked.
$RemoteDist = (Get-WslcAgentSetting WSLC_DEPLOY_DIST $RemoteDist -Required -What "the destination folder on the target, with forward slashes (C:/wslc/dist)") -replace '\\', '/'

$SshExe = Join-Path $env:SystemRoot "System32\OpenSSH\ssh.exe"
$ScpExe = Join-Path $env:SystemRoot "System32\OpenSSH\scp.exe"
if (-not (Test-Path $ScpExe)) {
    throw "OpenSSH scp not found at $ScpExe. Install 'OpenSSH Client' from Optional Features."
}

# The options every ssh and scp call shares: the jump host when there is one.
$Jump = if ($JumpHost) { @("-J", $JumpHost) } else { @() }
$Route = if ($JumpHost) { "$User@$RemoteHost via $JumpHost, port $Port" } else { "$User@$RemoteHost, port $Port" }

# Runs the MSI that is already on the target. Copying and installing are
# separate steps on purpose: the artifact is often there from an earlier run
# and only the install is wanted, and a copy is worth doing on its own when the
# machine is not free to install yet.
function Invoke-RemoteInstall {
    $msiRemote = ($RemoteDist -replace '/', '\') + "\$MsiName"
    $logRemote = ($RemoteDist -replace '/', '\') + "\WSLC-Agent-install.log"
    Write-Host "Installing the agent on $RemoteHost via msiexec (silent)" -ForegroundColor Cyan
    Write-Host "  MSI:        $msiRemote"
    Write-Host "  Properties: WSLC_BINDHOST=$BindHost WSLC_AGENTPORT=$AgentPort"
    Write-Host "  Runs as:    $User (SSH account)" -ForegroundColor DarkGray
    Write-Host "  Note: the MSI is per-user; an agent running in another account's session is not updated." -ForegroundColor Yellow

    # No spaces in the paths, so no quoting; the default remote SSH shell is cmd.
    $installCmd = "msiexec /i $msiRemote /qn /norestart " +
        "WSLC_BINDHOST=$BindHost WSLC_AGENTPORT=$AgentPort /l*v $logRemote"
    & $SshExe @Jump -p $Port -o StrictHostKeyChecking=accept-new "${User}@${RemoteHost}" $installCmd
    $rc = $LASTEXITCODE
    Write-Host "msiexec exit code: $rc"
    switch ($rc) {
        0 { Write-Host "Install completed on $RemoteHost." -ForegroundColor Green }
        3010 { Write-Host "Install completed on $RemoteHost; a reboot is required (3010)." -ForegroundColor Green }
        1619 { Write-Host "MSI not found on $RemoteHost ($msiRemote). Copy it first." -ForegroundColor Red }
        default { Write-Host "Install failed (exit $rc). Log on ${RemoteHost}: $logRemote" -ForegroundColor Red }
    }
}

# --- Install-only: run the already-deployed MSI on the target (no copy, no questions)
if ($Install) {
    Invoke-RemoteInstall
    return
}

# Resolve and validate every source file before touching the network.
$sources = @()
foreach ($name in $Files) {
    $path = Join-Path $DistDir $name
    if (-not (Test-Path $path)) {
        throw "Source artifact missing: $path (build it first)."
    }
    $sources += $path
}

# Choose what to copy: all, or pick file by file. -All skips the prompt.
$selected = @($sources)
if (-not $All) {
    Write-Host "Files available to copy:"
    foreach ($s in $sources) {
        $size = "{0:N1} MB" -f ((Get-Item $s).Length / 1MB)
        Write-Host ("  - {0} ({1})" -f (Split-Path $s -Leaf), $size)
    }
    # None is a real answer: the artifacts are often already there and only the
    # install is wanted, which the next question offers either way.
    $mode = Read-Host "Copy [A]ll, choose [O]ne by one, or [N]one? (A/O/N)"
    if ($mode -match '^[Nn]') {
        $selected = @()
    } elseif ($mode -notmatch '^[Aa]') {
        $selected = @()
        foreach ($s in $sources) {
            $ans = Read-Host "Copy $(Split-Path $s -Leaf)? (y/N)"
            if ($ans -match '^[Yy]') { $selected += $s }
        }
    }
}

# Copying and installing are independent: answer the install question whatever
# was copied, including nothing.
function Read-InstallAnswer {
    Write-Host ""
    $ans = Read-Host "Install $MsiName on $RemoteHost now? (y/N)"
    if ($ans -match '^[Yy]') { Invoke-RemoteInstall }
}

if ($selected.Count -eq 0) {
    Write-Host "Nothing to copy." -ForegroundColor DarkGray
    if (-not $All) { Read-InstallAnswer }
    return
}

# No quotes around the remote path: the path has no spaces, and modern scp
# (SFTP mode) would otherwise try to create a directory literally named with
# the quotes ("Bad message").
$dest = "${User}@${RemoteHost}:$RemoteDist/"

Write-Host "Deploying to $Route"
Write-Host "  Destination: $RemoteDist"
foreach ($s in $selected) {
    $size = "{0:N1} MB" -f ((Get-Item $s).Length / 1MB)
    Write-Host ("  - {0} ({1})" -f (Split-Path $s -Leaf), $size)
}

# Make sure the destination exists. Use PowerShell on the remote so this works
# whether the account's default SSH shell is cmd or PowerShell. The path has no
# spaces, so it needs no quoting. Redirect with cmd's >NUL (the default SSH
# shell is cmd), so no pipeline is parsed by the wrong shell.
$mkdirCmd = "powershell -NoProfile -Command New-Item -ItemType Directory -Force $RemoteDist >NUL 2>&1"
& $SshExe @Jump -p $Port -o StrictHostKeyChecking=accept-new "${User}@${RemoteHost}" $mkdirCmd

# Copy file by file (the key is installed, so no password prompts) so one
# locked destination does not abort the rest, and a retry only re-sends the
# files that failed instead of the whole set again. The common failure is
# "dest open ...: Failure" when the file is open on the target (the installer
# is running, or the .msi is open in Explorer / held by antivirus).
$pending = @($selected)
while ($pending.Count -gt 0) {
    $failed = @()
    $tunnelDown = $false
    foreach ($s in $pending) {
        $name = Split-Path $s -Leaf
        Write-Host "  Copying $name ..." -NoNewline
        # -O: the classic SCP protocol instead of SFTP. SFTP asks for a block and
        # waits for it, which on a link with latency (through a jump host and
        # down a reverse tunnel) leaves the line half idle. The artifacts are
        # already compressed, so -C would only add CPU.
        # scp's own words are kept: they tell a locked file ("dest open") from a
        # jump that could not reach the target ("connect failed", "stdio
        # forwarding failed"), which would otherwise read as the same thing.
        # Only its error stream goes to a file: the progress meter stays on the
        # screen after "Copying …", since a large installer down a tunnel takes
        # minutes and a silent copy reads as a hung one.
        $errorFile = [IO.Path]::GetTempFileName()
        $previousPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        # ServerAlive: a link that dies mid-copy (the tunnel dropping) fails in a
        # minute and is offered for retry, instead of a copy that hangs at 99%.
        & $ScpExe -O @Jump -P "$Port" -o StrictHostKeyChecking=accept-new -o ServerAliveInterval=15 -o ServerAliveCountMax=4 $s $dest 2> $errorFile
        $copied = $LASTEXITCODE -eq 0
        $ErrorActionPreference = $previousPreference
        $output = @(Get-Content $errorFile -ErrorAction SilentlyContinue)
        Remove-Item $errorFile -ErrorAction SilentlyContinue
        if ($copied) {
            Write-Host " OK" -ForegroundColor Green
        } else {
            Write-Host " FAILED" -ForegroundColor Red
            foreach ($line in $output) { if ($line.Trim()) { Write-Host "    $line" -ForegroundColor DarkGray } }
            $failed += $s
            if (($output -join "`n") -match 'stdio forwarding failed|connect failed|Connection refused|Connection closed by UNKNOWN') {
                $tunnelDown = $true
            }
        }
    }
    if ($failed.Count -eq 0) { break }

    Write-Host ""
    if ($tunnelDown -and $JumpHost) {
        Write-Host "The jump host could not reach the target: its reverse SSH tunnel ($JumpHost 127.0.0.1:$Port) is down." -ForegroundColor Yellow
        Write-Host "Nothing is locked. On the target, make sure its ssh tunnel task (vps-tunnels.ps1 -Status) is running" -ForegroundColor Yellow
        Write-Host "and bound: an ssh that stayed connected without its forward has to be closed" -ForegroundColor Yellow
        Write-Host "(on the jump host: ss -ltnp shows who holds :$Port, or nobody). Then retry:" -ForegroundColor Yellow
    } elseif ($tunnelDown) {
        Write-Host "The target could not be reached on port $Port. Nothing is locked. Then retry:" -ForegroundColor Yellow
    } else {
        Write-Host "Could not copy (the file is OPEN / locked on the target -- close the installer" -ForegroundColor Yellow
        Write-Host "or the file in Explorer there, then retry):" -ForegroundColor Yellow
    }
    foreach ($f in $failed) { Write-Host "  - $(Split-Path $f -Leaf)" -ForegroundColor Yellow }
    $answer = Read-Host "Press R to retry ONLY these (no full re-copy), or Enter to abort"
    if ($answer -notmatch '^[Rr]') {
        throw "Deploy aborted: $($failed.Count) file(s) not copied ($(if ($tunnelDown) { 'target unreachable' } else { 'still open on the target?' }))."
    }
    $pending = $failed
}

Write-Host "Done. Copied $($selected.Count) file(s) to ${RemoteHost}:$RemoteDist" -ForegroundColor Green

if (-not $All) { Read-InstallAnswer }
