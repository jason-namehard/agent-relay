# Agent Relay

**English** | [简体中文](README.zh.md)

[![CI](https://github.com/jason-namehard/agent-relay/actions/workflows/ci.yml/badge.svg)](https://github.com/jason-namehard/agent-relay/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/jason-namehard/agent-relay?include_prereleases&label=preview)](https://github.com/jason-namehard/agent-relay/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A local handoff desk for people who work with several AI agents: it records who handed what to whom, whether it was received, where the reply is, and who should continue.

When you move requests and drafts between agent sessions by copy and paste, it gets hard to tell which session received which message, whether it was actually picked up, and which reply belongs to which request. Agent Relay keeps each handoff in a local relay folder with a SQLite ledger, and shows its progress in a Windows desktop app. You choose the agents and their roles; Relay only needs a sender and a recipient. Manual copy-and-paste handoff works without any integration. Automatic delivery is optional and only available for agents whose calling method you have tested.

Status: public preview (`0.1.0-preview.9`). The Windows desktop app is the main entry point. CLI and stdio MCP are covered by Windows and Linux CI; the desktop app is Windows-only.

## Quick start (Windows x64)

Run in Windows PowerShell 5.1 or PowerShell 7, from any folder you want to download into (for example `Downloads`):

```powershell
$version = 'v0.1.0-preview.9'
$zip = "AgentRelay-$version-windows-x64.zip"
$ProgressPreference = 'SilentlyContinue'
Invoke-WebRequest "https://github.com/jason-namehard/agent-relay/releases/download/$version/$zip" -UseBasicParsing -OutFile $zip
Expand-Archive $zip -DestinationPath ".\AgentRelay-$version"
Set-Location ".\AgentRelay-$version"

# Isolated demo: sample agents and preset results, no model account, no model calls.
.\TryDemo.cmd
```

Extract the whole archive into a new folder; do not run files from inside the ZIP. You can also download the asset from the [release page](https://github.com/jason-namehard/agent-relay/releases/tag/v0.1.0-preview.9) and double-click `TryDemo.cmd`. Each demo run creates a fresh ledger under `%LOCALAPPDATA%\AgentRelay.Demos\` and does not read your existing agent configuration.

<details>
<summary>Verify the downloaded GitHub release asset (about 227 MB)</summary>

Run from the download folder before extracting:

```powershell
$expected = 'AA3CF3E2979EFDA4898C64CA3F54395FDC57673E3D133653E207EC188508B8C7'
if ((Get-FileHash $zip -Algorithm SHA256).Hash -ne $expected) {
    throw 'The archive does not match the v0.1.0-preview.9 Windows GitHub asset.'
}
```

This value identifies the Windows asset uploaded to GitHub, not every archive built from that source version. Local and CI builds are separate packages and are not claimed to be byte-for-byte reproducible.

The download and extraction commands were exercised with Windows PowerShell 5.1 against this complete GitHub asset. Development scripts below require PowerShell 7.

</details>

For real use, start the desktop app from the same folder:

```powershell
.\gui\AgentRelay.Desktop.exe
# Or keep a trial ledger separate from the default %LOCALAPPDATA%\AgentRelay:
.\gui\AgentRelay.Desktop.exe --home "$env:LOCALAPPDATA\AgentRelay-ManualTrial"
```

The desktop app and notification helper bundle their own .NET runtime. The CLI and MCP server (`relay.dll`, `mcp\relay-mcp.dll`) need the [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0). There is no installer and the binaries are not code-signed, so Windows SmartScreen or security software may warn before the first launch.

## How a handoff works

1. **Settings — confirm the relay folder.** The default is `%LOCALAPPDATA%\AgentRelay`. It holds requests, temporary replies and the ledger. You and every agent taking part must be able to read and write this folder.
2. **Agents — add two nodes.** Give each one any name you like. There are no built-in roles such as "frontend", "docs" or "reviewer". Host, config path and CLI path are optional and live under *Advanced*. If you want help filling the form, copy the setup prompt, give it to the agent, and paste its JSON reply back; parsing only fills the draft and you still save it yourself.
3. **Task board — create a handoff.** Pick the sender and recipient and write the request. You can also copy a generator prompt, let any agent draft the handoff, and paste its JSON reply; the dialog is filled in for review and nothing is created until you confirm.
4. **Deliver it.** The default route is manual: in the task details, copy the prompt for the recipient and paste it into that agent's session. The prompt contains the relay folder, the request and result paths, and the callback commands. If you have a verified automatic route for that direction (see [Optional: automatic CLI delivery](#optional-automatic-cli-delivery)), you can choose it instead.
5. **The recipient writes a reply and reports back explicitly** with the Relay CLI or MCP tools (`ack`, `start`, `complete` or `fail`). If the agent cannot run those, it gives you the reply file and you use **Import completed reply** in the task details and confirm.
6. **The sender continues.** When the task succeeds, copy the prompt for the sender from the task details and paste it into the sender's session.

A few rules keep the status honest:

- **Submitted is not completed.** A delivery receipt only means the request was handed over. A task becomes *succeeded* only after an explicit completion report or a reply import that you confirm.
- A result file appearing, being opened or being modified never completes a task by itself.
- The relay folder is a transit area. Managed files expire after the retention period (3 days by default); the cleanup action removes expired files when requested. Keep final work and files you will reference later in your project folder.

## What it includes

| Area | Current behaviour |
| --- | --- |
| Navigation | Four pages: Task board, Agents, Connections, Settings |
| Agent entry | One add/edit dialog; user-chosen names and roles; optional setup prompt with JSON parsing that fills the draft only |
| Handoff creation | Write directly, or have an agent draft it in a fixed JSON format; sender, recipient and content are reviewed before creation |
| Task status | SQLite ledger with explicit received, started, completed and failed events; request, reply and history shown together in the task details |
| Reply import | Import a reply file from the task details after confirming the recipient finished |
| Connection map | One line per agent pair; arrows show which directions have a verified calling recipe; colours show whether each direction is enabled |
| CLI extensions | Import an existing calling recipe (executable, argument array, input template, working directory, dependencies, source notes); import only saves it |
| CLI probe | Short marker request; machine receipt and your own visibility check are recorded separately; resuming the same session is tested separately |
| Automatic handoff | Uses a recipient's verified recipe and creates a new session; it does not reuse the probe session |
| Storage | Local relay folder with retention cleanup; moving to an empty folder copies and verifies, then switches, and keeps the old folder |
| Appearance | English, Simplified Chinese, Traditional Chinese, Japanese, Korean, Spanish; four colour themes and a custom accent colour |
| Notifications | Optional Windows notification helper included in the package; the Settings switch configures it |

| Component | Platform | Runtime | Automated checks |
| --- | --- | --- | --- |
| Desktop app (WPF) | Windows x64 | Bundled | Desktop interaction tests on `windows-latest` CI |
| Notification helper | Windows x64 | Bundled (includes Windows App SDK) | Settings save and toggle behaviour; real Notification Center pop-ups need a manual check |
| CLI and stdio MCP | Windows, Linux | .NET 10 runtime | Core tests on `windows-latest` and `ubuntu-latest` CI |
| macOS | No package | — | Not tested |

The relay folder is local. There is no cloud relay or hosted service. "No account" refers to Relay's own interface and the demo; the agents you connect still use their own accounts, permissions and model billing.

### Reading the connection map

| Line | Meaning |
| --- | --- |
| One arrow | Only the direction the arrow points to has a verified calling recipe |
| Two arrows | Both directions have a verified calling recipe |
| Grey dashed line, no arrow | Neither direction has a verified recipe |

A direction counts as verified once the recipient's recipe has passed a new-session probe and you have confirmed the session is visible. Green on an arrow and its half of the line means that direction is enabled; red means paused or not yet enabled. A two-way line can be green at one end and red at the other. Click a line to manage each direction. Manual copy is always available, whatever the line shows.

## Optional: automatic CLI delivery

Automatic delivery only works when the target agent already has a usable command-line interface or bridge program. Relay does not learn arbitrary CLIs on its own.

1. In **Settings → Advanced: CLI interface extensions**, copy the extension prompt and give it to an agent that knows how the target software can be called on your machine. Paste the returned JSON recipe. Importing saves it; nothing runs.
2. The recipe's command must hand the request to a real agent session and print one JSON object: `{"session_id": "...", "reply": "...", "accepted": true}`. If the native CLI does not print this, you need a separate bridge program that already exists and works. A recipe for a native CLI whose output does not match is not compatible.
3. In **Agents → Test CLI**, run a new-session probe. It sends a short marker. After the machine receipt passes, find the marker in the target app yourself and confirm that it is visible. A recipe with a resume command can also be tested for continuing the same session.
4. Enable that direction on the Connections page. *New handoff* then offers automatic CLI delivery for it.

What a passing probe does and does not prove:

- It records one result for that recipe in that environment. It does not guarantee that future sessions will be visible.
- A real automatic handoff starts a new session. It does not send work to the probe session.
- `accepted: true` means submitted. The recipient still has to report completion.
- Probes and handoffs call the real CLI and may incur model charges, including any context the host adds. Relay does not guarantee cache hits or token savings.
- Automated tests use local simulated bridges. They do not show that any particular host is connected.

## CLI and MCP

The Windows package and the `cli-mcp` package contain the same CLI (`relay.dll`) and stdio MCP server (`mcp/relay-mcp.dll`). Both need the .NET 10 runtime. CLI output and most longer docs are currently in Chinese; commands, flags and JSON fields are in English.

<details>
<summary>Minimal manual handoff from the command line</summary>

PowerShell, run from the extracted package folder with a new `.relay` directory. `dotnet` must resolve to a .NET 10 runtime; otherwise use the full path to that `dotnet.exe`. This is a local walkthrough with a sample reply; it does not invoke an agent or a model.

```powershell
$relay = Join-Path $PWD 'relay.dll'
$dataRoot = Join-Path $PWD '.relay'          # any folder both agents can reach
dotnet $relay --home $dataRoot init

# The CLI requires a config path. This fixture belongs to this manual walkthrough;
# it does not read or configure an installed agent host.
$config = Join-Path $dataRoot 'manual-node.json'
Set-Content $config '{"mode":"manual-walkthrough"}' -Encoding UTF8
dotnet $relay --home $dataRoot agent register --id writer   --config-path $config
dotnet $relay --home $dataRoot agent register --id reviewer --config-path $config

Set-Content .\request.md 'Review the attached notes.' -Encoding UTF8
$handoff = dotnet $relay --home $dataRoot task dispatch --from writer --to reviewer --request .\request.md --mode manual --idempotency-key review-1 | ConvertFrom-Json
$task = $handoff.data.task
$handoff.data.prompt           # give this to the reviewer agent

# The reviewer reports explicitly:
dotnet $relay --home $dataRoot task ack --task $task.task_id --attempt $task.attempt_id --sender reviewer --idempotency-key ack-1
# A sample reply for this walkthrough; use the actual recipient's reply in real work.
Set-Content $task.result_path 'Reviewed: the example handoff is complete.' -Encoding UTF8
dotnet $relay --home $dataRoot task complete --task $task.task_id --attempt $task.attempt_id --sender reviewer --status succeeded --result $task.result_path --idempotency-key done-1
dotnet $relay --home $dataRoot task status --task $task.task_id
```

Commands print JSON. Exit code `4` means saved but waiting for a manual step (the manual `dispatch` above returns it). Exit code `6` means the delivery or execution outcome is unknown; check the target session before retrying. Run any `relay` CLI command with `--help` for its options. On failure, use `task fail --status failed --error-code <code> --message <reason> --result <report>` with the same task, attempt, sender and idempotency key arguments.

</details>

<details>
<summary>stdio MCP server</summary>

The host starts one instance per agent, all pointing at the same relay folder:

```powershell
dotnet .\mcp\relay-mcp.dll --home <relay folder> --agent <registered agent id>
```

Tools: `relay_task_dispatch`, `relay_task_ack`, `relay_task_start`, `relay_task_complete`, `relay_task_fail`, `relay_task_status`, `relay_prompt_show`. The caller's agent identity is fixed at startup; dispatch selects the recipient through the `to` argument. Automatic delivery also checks the directed route permission. See [Phase 4](docs/PHASE4.md) for parameters and the reviewed config-migration flow.

</details>

<details>
<summary>Relay folder resolution and environment variables</summary>

The paths below show Windows defaults. On other systems, CLI/MCP use the platform's local application-data directory.

| Program | Order |
| --- | --- |
| CLI, MCP | `--home` → `RELAY_HOME` → `%LOCALAPPDATA%\AgentRelay` |
| Desktop app | `--home` → `RELAY_HOME` → folder chosen in Settings → `%LOCALAPPDATA%\AgentRelay` |
| Desktop demo | A new folder under `%LOCALAPPDATA%\AgentRelay.Demos\` |

If you move the relay folder in Settings, the CLI and MCP server do not follow automatically; pass the new `--home` or set `RELAY_HOME`, and copy handoff prompts again.

</details>

<details>
<summary>Advanced CLI routes</summary>

These are implemented in the CLI and documented in Chinese:

- AMQ transport and a one-shot Codex worker for read-only tasks — [Phase 2](docs/PHASE2.md), [Adapters](docs/ADAPTERS.md)
- Exact Codex session continuation and Windows notifications from the CLI — [Phase 3](docs/PHASE3.md)
- stdio MCP and reviewed JSON/TOML config migrations — [Phase 4](docs/PHASE4.md)
- Task lifecycle, events and file contracts — [Protocol](docs/PROTOCOL.md)

`scripts/verify-real-agents.ps1` drives real agent CLIs with your existing login and consumes model quota. Regular CI never runs it.

</details>

## Development and contributing

<details>
<summary>Build and test from source</summary>

Requires the .NET 10 SDK (see `global.json`). From the repository root in PowerShell:

```powershell
git clone https://github.com/jason-namehard/agent-relay.git
cd agent-relay
dotnet restore --locked-mode
dotnet test --configuration Release --no-restore
```

This builds and tests the solution: Core, CLI, MCP and `AgentRelay.Core.Tests`. The Windows desktop app and notification helper are built separately (Windows only, PowerShell 7):

```powershell
dotnet restore src/AgentRelay.Notify/AgentRelay.Notify.csproj --locked-mode
pwsh ./scripts/test-desktop.ps1
pwsh ./scripts/check-licenses.ps1 -IncludeDesktop
pwsh ./scripts/package.ps1 -Flavor Windows      # or -Flavor Portable for the CLI/MCP package
```

If `dotnet` on your `PATH` is not a .NET 10 SDK, pass its full path with `-Dotnet <path>` to `test-desktop.ps1`, `publish.ps1` and `package.ps1`, and call it directly for `restore` and `test`. Packages are written to `artifacts/packages/`; the script refuses to overwrite an existing archive.

</details>

<details>
<summary>Architecture</summary>

| Project | Role |
| --- | --- |
| `src/AgentRelay.Core` | Task lifecycle, SQLite ledger, managed workspace, delivery adapters, CLI extensions, relocation |
| `src/AgentRelay.Cli` | `relay` command line; JSON output |
| `src/AgentRelay.Mcp` | stdio MCP server bound to one agent per instance |
| `src/AgentRelay.Desktop` | WPF desktop app (four pages) |
| `src/AgentRelay.Notify` | Windows notification helper (Windows App SDK) |
| `tests/AgentRelay.Core.Tests`, `tests/AgentRelay.Desktop.Smoke` | Core tests; desktop interaction tests |

The CLI, MCP server and desktop app share one Core and one ledger, so a callback from an agent appears on the task board. The desktop app reads the ledger when it starts and when you refresh, and picks up changes from other Relay processes while it is open; it does not run recovery on start. Deadlines are processed the next time `relay recover` runs. More background: [Architecture](docs/ARCHITECTURE.md), [UI spec](docs/UI_SPEC.md), [Phase 5 desktop notes](docs/PHASE5.md). Older design documents also describe plans that are not implemented; the code and this README describe current behaviour.

</details>

Bug reports and focused improvements are welcome through [issues](https://github.com/jason-namehard/agent-relay/issues) and pull requests. Please include the version, platform, steps to reproduce, and redacted error codes; demo mode is useful for reproductions without credentials. See [CONTRIBUTING.md](CONTRIBUTING.md) (in Chinese), the [changelog](CHANGELOG.md) and the [release process](docs/RELEASING.md). Do not commit credentials, real agent configs, ledgers or private task logs.

## Known limitations

- Preview software: no installer, no code signing, Windows x64 only for the desktop app. No macOS desktop package, no cloud relay.
- Nothing runs after every Relay process has closed: there is no background service or scheduled task. Overdue tasks are handled on the next `relay recover`.
- Notifications: saving the switch is tested; whether Windows actually shows the banner in Notification Center depends on the system and needs a manual check. Behaviour at 125%/150% display scaling has not been fully reviewed.
- Automatic delivery needs a working CLI or bridge for each target and a passing probe in your environment. Compatibility with specific agent hosts is not claimed.
- Built-in exact session continuation from the CLI is for Codex. Other hosts use a tested CLI extension or manual copy.
- Cancelling a handoff stops Relay's further actions; it does not stop an agent that is already running. An unknown delivery is not re-sent automatically.
- Relocating the relay folder is covered by automated tests but has had little manual use.

## License

Agent Relay's code is released under the [MIT License](LICENSE). The Windows package also redistributes the .NET runtime, the Windows App SDK and other components under their own terms; see [DISTRIBUTION_TERMS.md](DISTRIBUTION_TERMS.md), [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) and the `licenses/` folder before redistributing.
