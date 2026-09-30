# Posts for X

A link counts as 23 characters and an emoji as 2; every post here fits in
280. Replace `<ARTICLE LINK>` with the article's link once it is published.

## Main post

Quote: https://x.com/craigaloewen/status/2104985841034506577
Attach: `media/wslc-ai-agent-mcp.mp4`

```text
WSL containers are GA, so we're open-sourcing WSLC AI Agent 🚀

A local agent + MCP server for WSLC, skill included: Claude, Hermes, OpenClaw & other AI agents manage your containers, from anywhere.

Web dashboard, Windows & Android apps. MIT.

https://github.com/Berpiztu/wslc-ai-agent
```

## Alternatives

Shorter, with hashtags:

```text
WSLC is GA 🎉 We built an MCP agent on top of it: tell Claude, Hermes or OpenClaw "run nginx on port 8080" and it happens on your WSL containers.

Open source, MIT 👇
https://github.com/Berpiztu/wslc-ai-agent

#WSL #WSLC #MCP #AIAgents
```

With the article:

```text
WSL containers just went GA. Here's what we built on top of it: an MCP agent that lets your AI agents run your WSLC containers, and a dashboard to watch them from anywhere.

The full story 👇
<ARTICLE LINK>
```

## The thread

A reply to the main post, right after it. Attach `media/wslc-ai-agent-overview.mp4`.

```text
What's inside:
• 53 MCP tools, destructive ones off by default
• A dashboard you design yourself, desktop & mobile
• Consoles on the host and in any container
• Publish a container on HTTPS through a built-in nginx proxy
• Self-updating, per-user installers, no admin rights
```

Second reply:

```text
Everything the app does, your AI agent can do by asking:

"List my containers"
"Run this as is: docker run -d -p 8080:80 nginx"
"Show me the logs of web"
"Publish port 80 of web on web.example.com"

Anything destructive asks for your yes first.
```

Last reply:

```text
Install it in one line, in PowerShell (and the same line updates it):

irm https://berpiztu.github.io/wslc-ai-agent/install.ps1 | iex

Or build it yourself. Stars, issues and PRs welcome ⭐
```

## Answers

Short answers to the questions likely to come, to reply fast.

**Is it free?**
Yes, open source under the MIT license.

**Does it replace Docker Desktop?**
It manages WSLC, Microsoft's own containers in WSL, which need no Docker at
all. It never calls `docker`. It even reads a pasted `docker run` line and
turns it into a WSLC one.

**What do I need?**
Windows 11 and WSL 3.0.1 or later (`wsl --update`). WSLC 2.9.13 works too.

**Which AI agents?**
Any MCP client. The skill installs with one click into Claude Code, Hermes
Agent and OpenClaw, on the same machine or another over SSH.

**Is it safe to let an AI run my containers?**
Destructive tools (remove, prune, kill, exec, publish) are off by default,
not even offered to the AI. Switched on, each one still asks for your yes
first.

**Can I use it from my phone?**
Yes: the Android app, or any browser. Remote access needs a login or an API
token; the repository has a guide for reaching it through your own VPS.

**Compose?**
Not yet: `wslc compose` is on Microsoft's roadmap, and we'll follow it.

**macOS, Linux, iOS?**
No: WSLC is Windows-only. The clients are Windows and Android, plus any
browser.

**How do I install it?**
One line in PowerShell: `irm https://berpiztu.github.io/wslc-ai-agent/install.ps1 | iex`.
The same line updates it later. The Windows client: `install-client.ps1`
instead of `install.ps1`.

**Why does Windows warn about the installers?**
It does not with the one-line install: PowerShell's download carries no
"from the internet" mark. Downloaded with a browser, it does: they are not
code-signed yet; the README says how to get past the warning.

**Who made it?**
Berpiztu, a team that builds AI agents and the tools they work with, with
Alex, an AI agent, alongside.
