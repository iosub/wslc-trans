# Owner notes

Private notes for running the public repository. **This file never goes to
the public repository**: leave it out of every copy from `wslc-trans` to
`Berpiztu/wslc-ai-agent`.

## The public repository

- **Berpiztu/wslc-ai-agent**, public since 30 September 2026 (the day WSLC
  became generally available). https://github.com/Berpiztu/wslc-ai-agent
- Local clone: `C:\IA\Berpiztu\wslc-ai-agent`. Open that folder in VS Code to
  work on it.
- Author of every commit there: `Iosu Buenetxea (Berpiztu) <iosub@berpiztu.ai>`.
  Every push and GitHub operation there as the GitHub account **Iosub-ai**
  (admin of the Berpiztu organisation). Both are set inside that clone
  (`git config` without `--global`), so VS Code, the terminal and the scripts
  use them without choosing.

## Two GitHub accounts on this machine

- **iosub**: wslc-agent, wslc-trans, wslc-dashboard and every other project.
  It is git's default for the whole machine
  (`git config --global credential.https://github.com.username iosub`).
- **Iosub-ai**: only `Berpiztu/wslc-ai-agent`, set in its clone.
- If Git Credential Manager ever shows a window asking which account to use,
  something lost that setting: the repository is using neither. Choose the
  account that owns that repository, and set it again in the repository:
  `git config credential.https://github.com.username <account>`.
- The `gh` command has both accounts, and uses the **active** one. For the
  public repository: `gh auth switch --user Iosub-ai`. For iosub's
  repositories: `gh auth switch --user iosub`. git itself does not depend on
  this.

## Private files in the public clone

The public clone needs the private files to build signed installers. Copy
them from `wslc-trans`, **without the two tracked files** of `private\`
(`README.md` and `env.example.psd1`), or `deploy-release.ps1` refuses to run
("Commit or discard these changes first"):

```powershell
Get-ChildItem C:\IA\wslc\wslc-trans\private -Exclude README.md,env.example.psd1 | Copy-Item -Destination C:\IA\Berpiztu\wslc-ai-agent\private\ -Force
```

If it happens anyway, restore them, in the public clone:

```powershell
git checkout -- private/README.md private/env.example.psd1
```

## Publishing a release

- Built **on this PC**, never on GitHub: the installers need the Android key
  and the Firebase files, which only live here.
- In the public clone, with nothing uncommitted:

  ```powershell
  .\deploy-release.ps1 -Publish
  ```

  It raises the version to the next patch (1.0.0 → 1.0.1), builds the agent
  MSI, the client MSI and the APK, commits "Release x.y.z", tags `vx.y.z`,
  pushes, and creates the GitHub release with the three installers.
  `-Version 1.1.0` chooses the version instead.
- The README's download links (`/releases/latest/download/...`) always give
  the newest release.
- The CI on GitHub only builds and tests; it makes no installer.
- **Automatic releases** (GitHub builds the installers when a tag is pushed,
  the key and the Firebase files as encrypted secrets) are possible and left
  to decide calmly: once the key is a secret, any admin of the repository
  could use it in a workflow.

### Next release: 1.0.1

Waiting for it, gathered since 1.0.0 (30 September 2026):

- The architecture diagrams play: the Live button and "Play story" work
  everywhere, also in a virtual machine or on a phone in power saving
  (`packaging/patch-archify-motion.mjs`). The public pages have it already;
  the app's own architecture button gets it with the release.
- The tray icon: a click opens the agent's page in the browser. Its own
  WebView2 window is gone: in it the dashboard's objects did not fit as they
  do in the browser.
- To look at: WSLC 3.0.1 no longer starts its session on its own. The
  agent's Set up (Settings > Publish) runs `wslc` without starting it, so on a
  stopped session it fails; `install.ps1` starts the session first
  (`POST /sessions/start`), the button should do the same. Check the other
  actions that assume a running session.
- Whatever shows up from the first users.

Run `.\deploy-release.ps1 -Publish` in the public clone: it takes 1.0.0 to
1.0.1 by itself.

## The Firebase key on your server

A published installer carries **no** Firebase key, on purpose: whoever
downloads it would get the power to push notifications through our Firebase
project. On the server that runs the agent, after installing, as the user
the agent runs as:

```powershell
Copy-Item C:\IA\wslc\wslc-trans\private\firebase-service-account.json "$env:LOCALAPPDATA\WSLC-AI-Agent\data\" -Force
```

Once: updates keep the data folder. **Settings → Notifications** shows
whether the agent found it.

## What GitHub costs

- Public repository: GitHub Actions (CI, Pages) is **free**, without limit.
- A private repository spends the organisation's monthly minutes (Windows
  counts double); without a payment method, the runs stop when they run out.
  **Settings → Billing and plans** of the organisation shows the use.

## How the public repository is set up

- `main` is protected: others change it through a pull request with one
  approval and CI green; as an admin you can still push directly (the
  release needs it). Force pushes and deleting `main` are blocked for
  everyone.
- Squash merge only; branches are deleted after merging.
- On: private vulnerability reporting (SECURITY.md sends reports there),
  secret scanning with push protection (a push carrying a key is refused),
  Dependabot alerts.
- GitHub Pages from GitHub Actions: `.github/workflows/pages.yml` publishes
  the four interactive architecture diagrams from
  `src/WslcAgent.UI/wwwroot/archify/` to
  https://berpiztu.github.io/wslc-ai-agent/architecture/, whenever they
  change. The README's animated images link there.

## The README's animated diagrams

`docs/images/architecture-overview.webp` and `architecture-mcp.webp` were
made from the Archify diagrams with the trace motion on: each frame captured
in headless Edge with the CSS animation paused at that instant, then encoded
by ffmpeg as animated WebP (a GIF of the overview weighed 31 MB). ffmpeg and
gitleaks are installed on this machine with winget. If a diagram changes, the
images have to be made again the same way.

## Before carrying changes to the public repository

- Leave this file and the `marketing/` folder out.
- Run gitleaks over the files that go (`gitleaks dir <folder>`), and search
  them for private names, domains and e-mails.
- Where development continues from now on (in the public repository, or here
  and carried over in batches) is still to decide.
