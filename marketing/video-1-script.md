# Video 1 — "Just tell your AI agent"

Recorded against the production agent (all its containers are test ones).
About four minutes. Rehearse every step before recording.

## Before recording

- Admin password long and unique (Settings → Security).
- Claude Code's MCP registration, with the token, done off camera:
  `claude mcp add --scope user --transport http wslc-agent https://<agent>/api/v1/mcp --header "Authorization: Bearer <TOKEN>"`
- Never on screen: the token, "Replace token", any client config file,
  the long-running Claude Code session (record a new one).
- Destructive tools off (Settings → MCP).

## Scenes

1. **The skill in Claude Code (0:00–0:40).** Settings → MCP: Serve MCP on,
   destructive tools off (hold two seconds). Skill section, Claude Code
   selected: copy the command, run it. New Claude Code session: `/mcp`
   shows wslc-agent connected.
   Caption: "One URL, one command: your AI agent now speaks WSLC."

2. **Claude Code at work (0:40–1:20).** "List my containers and tell me
   which ones use the most memory." Then "Run an nginx container called web,
   port 8080 to 80." Containers page: web downloading, then running.
   📸 the answer · 📸 web in the table

3. **Publish (1:20–1:50).** Publish web; open its public address
   (`<container>` + the agent's subdomain): the nginx page, from the
   internet.
   Caption: "Publish a container to the internet in one click."
   📸 the published page

4. **Hermes as a container (1:50–2:40).** Run the Hermes image
   (`nousresearch/hermes-agent:main`) from the agent; the row downloads,
   then runs. Hermes' own configuration is finished (cut or off camera).
   Then its skill: Settings → MCP → Skill, Hermes selected, the address
   the Hermes container reaches the agent by; copy the command and run it
   in the Hermes container's terminal (Exec).
   Caption: "Your AI agent can live in WSLC too."
   📸 Hermes running · 📸 the skill installed

5. **Side by side (2:40–3:40).** Claude Code on the left, Hermes on the
   right, both on the same agent. Ask each something different, for example
   Claude Code: "Which images do I have and how much disk do they use?";
   Hermes: "Show me the last 20 log lines of web." Then one change seen by
   both: Hermes restarts web, Claude Code is asked "Is web running? Since
   when?" and sees the restart.
   Caption: "Two AI agents, one WSLC. Any MCP client."
   📸 the two windows side by side

6. **Closing (3:40–4:00).** The MCP architecture page on GitHub Pages,
   animated. End text: github.com/Berpiztu/wslc-ai-agent, the one-line
   install, "Claude · Hermes · OpenClaw. Any MCP client."
