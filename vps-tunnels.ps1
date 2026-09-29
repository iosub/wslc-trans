#Requires -Version 5.1
<#
.SYNOPSIS
  Every reverse SSH forward from this PC to a VPS, kept open across reboots.

.DESCRIPTION
  A VPS in front of this PC (docs\developer\remote-access.md) reaches the agent,
  the published containers and this PC's own SSH only while a reverse forward
  from here holds a port on the VPS's loopback. Opened by hand, a forward dies
  with the session; this script registers one logon scheduled task per
  forward, each running its own ssh.exe -N -R and relaunched every minute when
  it exits. One task per forward on purpose: an ssh carrying several -R exits
  when any one of them fails to bind (ExitOnForwardFailure), and the others
  would go down with it.

  The forwards are the table below. Adding one is one more row.

    agent      VPS 127.0.0.1:8069 -> here 127.0.0.1:8069   the agent
    published  VPS 127.0.0.1:8081 -> here 127.0.0.1:8081   the proxy of the published containers
    ssh        VPS 127.0.0.1:2222 -> here 127.0.0.1:22     sshd on this PC (deploy-server.ps1 through the VPS)

  The VPS comes from WSLC_TUNNEL_HOST, in the environment or in
  private\env.psd1 (docs\developer\environment.md); -VpsHost wins over both.
  It is reached with a key: the script never asks for a password.

  Modes:
    (default)   Register (or replace) the tasks and start them.
    -Status     Every forward: task state, local ssh, local listener, VPS listener.
    -Uninstall  Stop every ssh and remove every task.

  -Name limits any mode to one forward (e.g. -Name published -Status).

.PARAMETER VpsHost
  SSH destination, user@host, key-authenticated. Default: WSLC_TUNNEL_HOST.

.PARAMETER Name
  One forward's name from the table: agent, published or ssh. Default: all.
#>
[CmdletBinding()]
param(
    [string]$VpsHost,
    [ValidateSet("agent", "published", "ssh")]
    [string]$Name,
    [switch]$Status,
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptDir "packaging\Packaging.ps1")
Import-WslcAgentPrivateSettings
$VpsHost = Get-WslcAgentSetting WSLC_TUNNEL_HOST $VpsHost -Required -What "the VPS the reverse forwards go to, user@host, reached with a key"

$Forwards = @(
    @{ Name = "agent";     RemotePort = 8069; LocalPort = 8069; What = "the agent" }
    @{ Name = "published"; RemotePort = 8081; LocalPort = 8081; What = "the proxy of the published containers" }
    @{ Name = "ssh";       RemotePort = 2222; LocalPort = 22;   What = "sshd on this PC" }
)
$SshExe = Join-Path $env:SystemRoot "System32\OpenSSH\ssh.exe"

function Get-Forward-Spec($forward) {
    # The literal 127.0.0.1 on both ends: "localhost" may resolve to ::1, and
    # the VPS port must never be 0.0.0.0, which would open it to the internet.
    $rule = "$($forward.RemotePort):127.0.0.1:$($forward.LocalPort)"
    @{
        TaskName = "WSLC-Tunnel-$($forward.Name)"
        Rule     = $rule
        SshArgs  = "-N -R $rule " +
                   "-o ServerAliveInterval=30 -o ServerAliveCountMax=3 " +
                   "-o ExitOnForwardFailure=yes -o BatchMode=yes -o ConnectTimeout=15 " +
                   "-o StrictHostKeyChecking=accept-new $VpsHost"
    }
}

function Get-ForwardProcesses($rule) {
    # Only the ssh carrying this rule, whoever started it.
    Get-CimInstance Win32_Process -Filter "Name = 'ssh.exe'" |
        Where-Object { $_.CommandLine -like "*-R $rule *" -or $_.CommandLine -like "*-R $rule" }
}

function Stop-ForwardRuntime($spec) {
    $task = Get-ScheduledTask -TaskName $spec.TaskName -ErrorAction SilentlyContinue
    if ($task -and $task.State -eq "Running") { Stop-ScheduledTask -TaskName $spec.TaskName }
    foreach ($proc in Get-ForwardProcesses $spec.Rule) {
        Stop-Process -Id $proc.ProcessId -Force
        Write-Host "Stopped ssh pid $($proc.ProcessId) (-R $($spec.Rule))"
    }
}

function Remove-Task($taskName) {
    if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) {
        if ((Get-ScheduledTask -TaskName $taskName).State -eq "Running") { Stop-ScheduledTask -TaskName $taskName }
        Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
        Write-Host "Removed task $taskName"
    }
}

function Test-VpsListener($remotePort) {
    # $true / $false, or $null when the VPS could not be asked.
    try {
        $out = Invoke-WslcAgentNative $SshExe @("-o", "BatchMode=yes", "-o", "ConnectTimeout=10", $VpsHost, "ss -ltn | grep -q '127.0.0.1:$remotePort ' && echo UP || echo DOWN")
        if ($LASTEXITCODE -ne 0) { return $null }
        return ($out -join '') -match 'UP'
    } catch {
        return $null
    }
}

function Register-Forward($forward) {
    $spec = Get-Forward-Spec $forward
    Stop-ForwardRuntime $spec
    Remove-Task $spec.TaskName
    # ssh exits when the connection drops or the VPS is not reachable yet
    # (right after logon, typically); the scheduler relaunches it every
    # minute, and never kills it for running long.
    $settings = New-ScheduledTaskSettingsSet `
        -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable `
        -MultipleInstances IgnoreNew -ExecutionTimeLimit ([TimeSpan]::Zero) `
        -RestartCount 999 -RestartInterval (New-TimeSpan -Minutes 1)
    Register-ScheduledTask -TaskName $spec.TaskName `
        -Action (New-ScheduledTaskAction -Execute $SshExe -Argument $spec.SshArgs) `
        -Trigger (New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME) `
        -Principal (New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Limited) `
        -Settings $settings `
        -Description "WSLC AI Agent: reverse SSH forward $($spec.Rule) to $VpsHost for $($forward.What)" `
        -Force | Out-Null
    Write-Host "Registered $($spec.TaskName): ssh.exe $($spec.SshArgs)"
    Start-ScheduledTask -TaskName $spec.TaskName
}

function Wait-ForwardUp($forward) {
    $spec = Get-Forward-Spec $forward
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline) {
        if ((Get-ForwardProcesses $spec.Rule) -and (Test-VpsListener $forward.RemotePort) -eq $true) {
            Write-Host "$($forward.Name): up, VPS 127.0.0.1:$($forward.RemotePort) -> this PC 127.0.0.1:$($forward.LocalPort)"
            return
        }
        Start-Sleep -Seconds 2
    }
    Write-Warning "$($forward.Name): not confirmed on the VPS yet. Check: vps-tunnels.ps1 -Status"
}

function Show-ForwardStatus($forward) {
    $spec = Get-Forward-Spec $forward
    Write-Host "== $($forward.Name): -R $($spec.Rule) -> $VpsHost ($($forward.What))"
    $task = Get-ScheduledTask -TaskName $spec.TaskName -ErrorAction SilentlyContinue
    if ($task) {
        $info = Get-ScheduledTaskInfo -TaskName $spec.TaskName
        Write-Host "   Task:      $($spec.TaskName) ($($task.State), last result $($info.LastTaskResult), as $($task.Principal.UserId))"
    } else {
        Write-Host "   Task:      $($spec.TaskName) (not installed)"
    }
    $procs = @(Get-ForwardProcesses $spec.Rule)
    if ($procs.Count -gt 0) {
        Write-Host ("   Local ssh: pid(s) {0}" -f (($procs | ForEach-Object { $_.ProcessId }) -join ", "))
    } else {
        Write-Host "   Local ssh: not running"
    }
    # The agent and nginx bind 127.0.0.1 exactly; sshd binds every address
    # (0.0.0.0), which also answers on the loopback.
    $listening = [bool](Get-NetTCPConnection -LocalPort $forward.LocalPort -State Listen -ErrorAction SilentlyContinue |
        Where-Object { $_.LocalAddress -in @("127.0.0.1", "0.0.0.0") })
    Write-Host "   Local:     127.0.0.1:$($forward.LocalPort) $(if ($listening) { 'listening' } else { 'NOT listening (nothing to forward to)' })"
    $up = Test-VpsListener $forward.RemotePort
    if ($null -eq $up) {
        Write-Host "   VPS:       unreachable or ssh prompted (key/host key?)"
    } elseif ($up) {
        Write-Host "   VPS:       127.0.0.1:$($forward.RemotePort) bound"
    } else {
        Write-Host "   VPS:       127.0.0.1:$($forward.RemotePort) NOT bound"
    }
}

if ($Status -and $Uninstall) { throw "Choose one of -Status, -Uninstall." }
if (-not (Test-Path $SshExe)) { throw "ssh.exe not found at $SshExe" }

$selected = if ($Name) { $Forwards | Where-Object { $_.Name -eq $Name } } else { $Forwards }

if ($Status) {
    foreach ($forward in $selected) { Show-ForwardStatus $forward }
} elseif ($Uninstall) {
    foreach ($forward in $selected) {
        $spec = Get-Forward-Spec $forward
        Stop-ForwardRuntime $spec
        Remove-Task $spec.TaskName
    }
} else {
    foreach ($forward in $selected) { Register-Forward $forward }
    foreach ($forward in $selected) { Wait-ForwardUp $forward }
}
