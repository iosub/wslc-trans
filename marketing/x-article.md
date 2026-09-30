# X article

Cover: `media/article-cover.png` (1500×600). Images where marked.

---

## Title

Your AI agents can now run your Windows containers

## Subtitle

WSL containers went GA. We open-sourced WSLC AI Agent the same day: an MCP
server, with its skill included, that lets Claude, Hermes, OpenClaw and
other AI agents manage WSLC, and a dashboard to watch it all from anywhere.

---

Yesterday Microsoft made WSL containers, WSLC, generally available: Linux
containers running natively on Windows, built into WSL, with no Docker
Desktop in between. It is a big step for everyone who develops on Windows.

We had been building on WSLC since its preview. The same day it went GA, we
released what we built: **WSLC AI Agent**, open source under the MIT
license.

https://github.com/Berpiztu/wslc-ai-agent

### An agent for WSLC, built for AI agents

WSLC AI Agent is a small Windows program that runs beside `wslc`, in the
background of your Windows session, and does the work for everyone who asks:
the web dashboard, the Windows and Android apps, a REST API, and an **MCP
server**.

That last one is the point. With MCP, any AI agent that speaks it can manage
your containers: 53 tools covering containers, images, volumes, networks,
sessions, publishing, logs and notifications. And we ship the **skill** that
teaches the agent how WSLC works, installed with one click into Claude Code,
Hermes Agent or OpenClaw, on the same machine or on another over SSH.

So instead of remembering flags, you ask:

> "Run this as is: docker run -d --name web -p 8080:80 nginx:latest"
>
> "Show me the last 20 log lines of the web container"
>
> "Create a volume called site-html and run nginx on port 8081 with it"
>
> "Show me the network topology"

[Image: `media/article-mcp.png`: how AI agents reach WSLC]

### Safe by default

Letting an AI touch your machine deserves care. The destructive tools
(removing, pruning, killing, running a command inside a container,
publishing, stopping a session) are **off by default**: the AI agent is not
even offered them. When you switch them on, each one still stops and asks
for your yes, showing exactly what it is about to do.

### Easy container management, for you too

The same agent serves a full web interface, built with Blazor and MudBlazor:

- Containers, images, volumes, networks and sessions, as tables or cards.
- Paste a `docker run` or `wslc run` line and the form fills itself in,
  checked before anything runs: names in use, ports taken, missing networks.
- Live CPU, memory, disk and network charts, a live network map, and restart
  policies the agent keeps by itself.
- A file browser inside containers, images and volumes, with transfers that
  keep going when you close the window.
- Every `wslc` command the agent ran, with its output, on the Logs page.

### Manage from anywhere

Not only the WSLC machine: its containers too.

- **Consoles**: the host's terminal and a shell in any container, in the
  browser.
- **A container's web page**, even when it is not published, through a
  browser that runs on the agent's machine and streams to you.
- **Publishing**: put a container's port on a public HTTPS name through a
  built-in nginx proxy, from the container's own form.
- **The Android app**, the Windows app or any browser, with a login or an API
  token from outside your network, and a guide for reaching it through your
  own VPS with no port opened at home.

[Image: `media/article-overview.png`: the whole architecture]

### A dashboard you design yourself

The Home page is yours to design: drag live objects onto a grid (readings,
dials, charts, containers, logs), group them into cards, and set alarms
that turn a card red. A landscape view for the desktop and a portrait one
for the phone, kept on the agent for every device, or on each device alone.

### Easy to start

- **Install it**: per-user installers, no administrator rights, from the
  latest release. The agent starts at logon and updates itself.
- **Or build it yourself**: one script tells you what your machine lacks,
  another installs it all, a third builds and tests.

It needs Windows 11 and WSL 3.0.1 or later (`wsl --update`).

### Open source, and just the beginning

WSLC AI Agent is MIT-licensed and on GitHub. Try it, star it, open an issue
with what you miss, or send a pull request: the contributing guide and an
`AGENTS.md` are there for people and AI agents alike.

https://github.com/Berpiztu/wslc-ai-agent

Built by Berpiztu, a team that builds AI agents and the tools they work
with. And with Alex, the AI agent who helped create it.
