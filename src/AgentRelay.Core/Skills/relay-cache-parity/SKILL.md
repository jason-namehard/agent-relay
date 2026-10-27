---
name: relay-cache-parity
description: Use when registering or checking an Agent's call recipe for Agent Relay, or when Relay-continued sessions burn noticeably more tokens. Aligns the request Relay sends when it continues a session (through the command line or ACP) with the request the Agent's own desktop sends, so switching between them keeps hitting the provider's prompt cache.
---

# Cache parity for continued sessions

## Why

Relay continues the user's existing sessions so the provider can reuse its prompt cache. That only works when the new request starts with exactly the same bytes as the previous one: tool definitions, system prompt, then history.

Many hosts rebuild the tool set and the system prompt every time they start. A desktop turn and a command-line turn in the same session can therefore send different prefixes. Each time the user switches between them, the whole history is billed again at full price.

Measured on Zcode before alignment: the first turn after each switch read 0% from cache, although consecutive turns on the same side read 99%. After alignment, switches read 93–99%.

## When to use

- After importing a call recipe during one-click setup. Static checks are always required; experiments need the user's consent.
- When the user reports that Relay-continued sessions cost much more than desktop ones.
- After the host was upgraded, or the user changed a desktop setting that may affect the prompt.

## Rules

- Make the command line follow the desktop: its own config files, its settings, its data folder. Never change the desktop's behaviour to suit the command line.
- Before editing any user config file, back it up and tell the user the path. Explain any side effect on the desktop.
- Never read, copy, print or store credentials. Do not patch the host's installed program files.
- Real model calls cost the user money. Before an experiment, tell the user how many turns, which Agent, and which session, and get consent.
- Do not write into a real conversation unless the user named it for the test. Otherwise create a new test session, and tell the user afterwards which sessions to delete.

## Steps

1. **Classify the host.** The type decides what is possible.
   - **Gateway (type A).** The command line or ACP process only attaches to the same background process the desktop uses, which builds the request. Often the session's system prompt and tool set are frozen when the session is created. Requests already match. Only check that continuing uses `session/load` or `session/resume` with the exact session ID, the session's own working directory, and no extra MCP servers.
   - **Rebuild per launch (type B).** The command line starts its own process and builds tools and prompt itself. Every launch difference must be aligned (steps 4–6).
   - **Cannot continue desktop sessions (type C).** The command line refuses or rebuilds desktop-created sessions. Tell the user plainly, record it in the recipe's `limitations`, and keep manual handoff for those sessions.
2. **Find the per-request usage record:** input tokens and cache-read tokens per model request, in a database table, a log, or the `--json` usage events.
   - Relay reads it for known hosts: `relay agent cache-check --agent <agent id> --session <session id>` (Zcode, Hermes).
   - For other hosts, find the source and report it to the user and to the Relay maintainers.
3. **Find the full outgoing request,** so you can compare the two sides exactly:
   - The host may save model requests per session.
   - It may have a debug dump switch.
   - The session log may record the tools and system prompt it sent.
   - Otherwise, log at the local provider gateway or proxy if the user runs one.
4. **Run the comparison** (with consent): a desktop turn, a Relay turn within a few minutes, another desktop turn, another Relay turn. Run `cache-check` after each.
   - A high hit on consecutive turns of one side, but a sharp drop right after a switch, means the prefixes differ.
   - A drop after a long gap may only be cache expiry. Repeat with turns a few minutes apart before concluding.
5. **Compare the two requests field by field:** the tool list (names and definitions), each system message, and any notes, skill lists or environment blocks inserted into the history. The first difference is the next thing to align. Common causes:
   - tool loading mode (all tools at once vs on demand);
   - surface or client identifier (desktop vs terminal, platform name);
   - feature switches that add skills or tools (workflows, memory, plugins, MCP servers);
   - permission preset text;
   - working directory, model and reasoning level;
   - values that change on every launch (random ports, timestamps).
6. **Align**, in this order of preference:
   1. a command-line flag or environment variable;
   2. pointing the command line at the desktop's own config file;
   3. stacking the desktop's profile or patch layers;
   4. as a last resort, a shared config setting (with a backup, and the side effect explained to the user).

   Re-test after each change, until the requests match byte for byte before the newest message.
7. **Make it stick.** Put the alignment into the recipe and import it again. Run `relay cli-recipe doctor --agent <agent id>`; every `host.parity.*` check must pass. If Relay does not know this host yet, write the alignment items and the measured result into the recipe's `limitations`, so the next maintainer can build them in.
8. **Report** to the user:
   - hit rates before and after;
   - which files changed, and where the backups are;
   - any difference that cannot be aligned, and what it costs.

## Known hosts

### Zcode: type B, built into Relay

Requests are saved per session in `~/.zcode/cli/rollout/model-io-<session>.jsonl`. Usage is in `~/.zcode/cli/db/db.sqlite`, table `model_usage`. The desktop's settings are in `~/.zcode/v2/setting.json`. Its data folder is `<dataBaseDir>\.zcode\v2` when moved, otherwise `~/.zcode/v2`.

When a Zcode recipe is imported, Relay adds:

| Item | What Relay sets |
| --- | --- |
| Tool loading | `--toolsearch-mode local` |
| Surface | `--surface desktop` |
| Workflows | `--workflow-mode` taken from the desktop's `dynamicWorkflowMode` |
| Generated UI | `ZCODE_GEN_UI_OUTPUT_ROOT=<data folder>\visualizations` |

Doctor also checks two things:

- **Memory.** The command line reads `features.memory` from `~/.zcode/cli/config.json`, and so does the desktop. Set it to `false` only when the desktop's memory is off.
- **Provider config.** `ZCODE_PERSONAL_PROVIDER_CONFIG_FILE` must point at the desktop's `provider_config.json`.

### Hermes: type A

The `hermes acp` process attaches to the desktop's gateway, which runs every turn. Each session's system prompt and pinned tools are stored in `state.db` and reused, so a continued session sends the same prefix from either side. A different platform only adds a short note at the end of the request.

What can break it:
- **A different working directory.** The gateway moves the session and rebuilds its prompt. Relay passes the session's own `cwd` when it continues one.
- **Extra MCP servers.** These change the tools. Relay sends none.
- **Slash commands** such as `/model` or `/compress`. These rebuild the prompt.

Sessions created by Relay through `session/new` are ACP sessions. Their tool set differs from desktop-created ones, but it stays the same whichever side continues them.

- Usage: `HERMES_HOME\logs\agent.log`, one `API call #` line per model request (`cache=<read>/<input>`).
- Full requests: set `HERMES_DUMP_REQUESTS=true` in the gateway's environment (`HERMES_HOME\.env`), then restart the gateway. Dumps go to `HERMES_HOME\sessions\request_dump_*`.

### DSH (DeepSeek Harness): type C through headless; type A through the running desktop

**Headless cannot match the desktop.** Relay's DSH recipe runs `--profile headless`. Desktop sessions are created under agent preset `standard`, which adds desktop-only tools: the desktop sends 42 tools against headless's 25, and its system prompt is about 1,800 tokens against 700.

- **Headless cannot continue a desktop session.** Its runner refuses any session that has a preset.
- **The desktop changes the tool set of a session Relay created.** Such a session has no preset, so when the desktop continues it, the `standard` preset applies and the tool list changes. The first desktop turn misses the whole cache; later turns on the same side hit again.
- **Patch layers cannot close this gap.** The preset is mounted only by the desktop's web-app composition, and the desktop profile cannot be started from the command line.

**The aligned route is the running desktop host.** The desktop serves an HTTP/WebSocket API on `127.0.0.1` with a random port. The turn is then built by the desktop process itself, so the requests are identical:

- `POST /api/session/create` with `{cwd, agentPreset:"standard"}`
- `POST /api/session/prompt` with `{requestId, sessionId, mode:"queue", content:[{type:"text",text}], clientTimeZone}`
- the `session/follow` stream on `ws://…/api/remote.mux`, finished at `turn/end`

Authentication is a cookie signed with a secret from the user's credential file, or a per-launch token. Whichever way, it needs the user's decision. Do not read the credential file on your own.

**Where to look:**
- **Usage:** the session log `DSH_HOME\sessions\<cwd>\session-<id>\session.v4.jsonl.zstd`. The file is a series of zstd frames; split it at `28 b5 2f fd`. Read `assistant/message` → `data.usage.cacheReadTokens`. In DSH, `inputTokens` is only the uncached part.
- **Full request:** the same log. `request/header` holds the tool schemas, and `system/message` holds the system prompt.
- **Session totals:** `DSH_HOME\storages\session_projcache\sessions\<id>.json`.
