# Environment variables

Nothing about your machines is written in the repository's scripts: every
host, account, folder and key location they need comes from a variable. This
page lists them all.

## Where to set them

The easiest place is `private\env.psd1`, which git never tracks. Copy the
example once:

```powershell
Copy-Item private\env.example.psd1 private\env.psd1
```

Then open `private\env.psd1` and fill in only what you use; an empty value
sets nothing. The file is read as data, never run.

The same names also work as ordinary environment variables. When both are
set, the environment wins over the file, and a parameter given on a script's
command line wins over both. A script that needs a variable nobody set stops
and names it.

## Build and signing

Read by `build-client-apk.ps1`, `build-client-installer.ps1`,
`build-agent-installer.ps1` and `check-private.ps1`. All optional: by default
the files are looked for in `private\` ([private-files.md](private-files.md)).

| Variable | Default | What it is |
|---|---|---|
| `WSLC_AGENT_KEYSTORE` | `private\android.keystore`, then `%USERPROFILE%\.wslc-agent\android.keystore` | The Android signing key, when it is kept elsewhere |
| `WSLC_AGENT_KEYSTORE_PASS` | The keystore's `.pass` file | Its password |
| `WSLC_AGENT_KEY_ALIAS` | `wslc-agent` | The key's alias inside the keystore |
| `WSLC_AGENT_KEY_PASS` | The keystore's password | The key's own password, when it differs |
| `WSLC_AGENT_GOOGLE_SERVICES` | `private\google-services.json` | The Android client's Firebase configuration |
| `WSLC_AGENT_PUSH_KEY` | `private\firebase-service-account.json` | The Firebase key the agent installer carries |

## Deploying to another machine

Read by `deploy-server.ps1`, which copies the built installers to the machine
that runs the agent over SSH and can install the agent there.

| Variable | Default | What it is | Example |
|---|---|---|---|
| `WSLC_DEPLOY_USER` | required | The Windows account on the target used for SSH | `builder` |
| `WSLC_DEPLOY_HOST` | required | The target as SSH reaches it; behind a jump host, the address on the jump host's side | `agent-pc`, or `127.0.0.1` behind a jump host |
| `WSLC_DEPLOY_PORT` | `22` | The SSH port on that host | `2222` behind a jump host |
| `WSLC_DEPLOY_JUMP` | none | An SSH jump host, `user@host`, when the target is not reachable directly | `user@vps.example.com` |
| `WSLC_DEPLOY_DIST` | required | The destination folder on the target, with forward slashes | `C:/wslc/dist` |

The target needs the OpenSSH server and your public key in its authorized
keys. For an account that is a local administrator there, that is
`C:\ProgramData\ssh\administrators_authorized_keys`, not the file in its own
profile.

A target behind a VPS: [remote-access.md](remote-access.md) sets up the
reverse tunnel that makes `WSLC_DEPLOY_HOST=127.0.0.1`,
`WSLC_DEPLOY_PORT=2222` and `WSLC_DEPLOY_JUMP=user@vps.example.com` work.

## Tunnels to a VPS

Read by `vps-tunnels.ps1`, which keeps the reverse SSH forwards from this PC
to a VPS open across reboots ([remote-access.md](remote-access.md)).

| Variable | Default | What it is | Example |
|---|---|---|---|
| `WSLC_TUNNEL_HOST` | required | The VPS, `user@host`, reached with a key | `user@vps.example.com` |

## The agent itself

The agent reads none of these. Its own settings (bind address and port) are
chosen when it is installed, and its Publishing settings (domain, name
suffix, proxy, network, map file) are set in its UI, **Settings →
Publish**.

One setting of the agent is worth knowing on a machine that builds the
installers: where the agent finds the client installers it offers to the
clients for updating themselves, and its own installer for its self-update.
It is `Wslc:ClientPackagesPath`; an installed agent has no `dist` folder of its
own, so without it the agent offers no update. Set it for your user, pointing
at the `dist` folder the build scripts write to, then restart the agent:

```powershell
[Environment]::SetEnvironmentVariable("Wslc__ClientPackagesPath", "C:\src\wslc-ai-agent\dist", "User")
```
