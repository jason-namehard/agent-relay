# Agent Relay

**English** | [简体中文](README.zh.md)

[![CI](https://github.com/jason-namehard/agent-relay/actions/workflows/ci.yml/badge.svg)](https://github.com/jason-namehard/agent-relay/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A local handoff desk for people working with several AI agents. Pass a file from A to B, see who needs to act next, and finish without unnecessary back-and-forth.

## Quick start — Windows x64

Public preview: `0.1.0-preview.14`. In Windows PowerShell 5.1 or PowerShell 7, download and extract the whole package into a new folder:

```powershell
$version = 'v0.1.0-preview.14'
$zip = "AgentRelay-$version-windows-x64.zip"
$download = Join-Path $PWD $zip
$ProgressPreference = 'SilentlyContinue'
Invoke-WebRequest "https://github.com/jason-namehard/agent-relay/releases/download/$version/$zip" -UseBasicParsing -OutFile $download
Expand-Archive -LiteralPath $download -DestinationPath ".\AgentRelay-$version"
Set-Location ".\AgentRelay-$version"
.\gui\AgentRelay.Desktop.exe
```

Or download from [Releases](https://github.com/jason-namehard/agent-relay/releases/tag/v0.1.0-preview.14). Start the executable above to configure real Agents. `TryDemo.cmd` opens isolated samples and blocks real CLI calls; Settings can keep that workspace and switch it to normal use. GUI and notifications bundle their runtime. CLI/MCP require [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0). There is no installer or code signing; Windows may show a security warning.

<details>
<summary>Verify the uploaded GitHub asset</summary>

In the same PowerShell session:

```powershell
$release = Invoke-RestMethod "https://api.github.com/repos/jason-namehard/agent-relay/releases/tags/$version"
$asset = $release.assets | Where-Object name -eq $zip
if (-not $asset.digest) { throw 'The release API did not provide an asset digest.' }
$actual = 'sha256:' + (Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actual -ne $asset.digest) { throw 'The file does not match the GitHub asset.' }
```

Independent local and CI builds are not claimed to be byte-for-byte reproducible.

</details>

## Start a CLI task

1. **Configure A and B** in Agents with **One-click setup** or the manual fields. CLI delivery needs a real calling method.
2. **Choose New CLI task** on the task board. Select A and its source conversation, select B, and choose a new or existing target conversation. Write the work request directly; Relay prepares the prompt and invokes the selected CLI. No preliminary handoff file or JSON is required.
3. **Choose the completion plan.** A single-step task ends when B finishes. Enable **recipient → sender → recipient** for B to work, A to review and B to apply. An early completion receipt cannot skip a required step; every planned return needs a real result file and concrete next action.
4. **Create and send through CLI.** Explicitly allow any required directions that are not enabled yet. This starts a real host call and may incur model charges. The board names the current worker and updates from receipts; later turns resume the exact bound conversations.

Unconfirmed delivery is not automatically retried. A missing session requires another explicit choice, never a fallback to the latest conversation. A CLI response alone does not prove the work is complete. [Session selection and limits](docs/SESSIONS.md).

<details>
<summary>Pass an existing file manually</summary>

### A file handoff in practice

1. **Configure A and B** in Agents. Choose **One-click setup**, then paste the copied prompt into a new conversation with the Agent to register. It inspects its environment and submits configuration directly. Each prompt creates a fresh registration request. **New Agent** provides separate manual fields; only a name is required.
2. **Optionally install the handoff rule.** Choose the instruction file the host actually loads, inspect the one-line addition and confirm. Detailed instructions are read when needed; existing sessions may need to reread their instructions.
3. **Ask A to write a file for B.** The installed rule can make A return short handoff JSON. Without a rule, append the format copied from New handoff to your request. Paste the JSON and choose **Parse and create handoff**, or select a file yourself.
4. **Choose B's conversation.** When a CLI method is configured, choose a new conversation or explicitly select an existing one. The picker refreshes only that Agent and never chooses a row for you. A clipboard handoff cannot identify A's source conversation automatically; select it once if you want CLI replies to return there.
5. **Deliver the prompt to B.** Relay uses the registered CLI method when the A → B direction is enabled; otherwise, choose **Copy prompt for B**. The short prompt points to the file, work request and receipt template.
6. **B writes a receipt.** While the app is open, receipt file events update the task. Startup and Refresh also read outstanding receipts; no manual reply import is required for this workflow.
7. **Follow the completion plan.** The file flow also offers the review-and-apply plan. A required step cannot be skipped by `needs_reply: false`; an intermediate receipt needs a real result file and concrete `next_action`. Without a plan, `done` with `needs_reply: false` ends the task, but cannot also request further work in `next_action`. Relay resumes the bound conversation or offers the prompt for the other agent. After four turns it stops for a human decision.

While an Agent is processing, the board tells you to wait for its receipt. A CLI submission is recorded once per turn; if delivery is unconfirmed, inspect the target session before sending manually. Within the same task attempt, later turns reuse each Agent's exact bound session ID. A missing session stops automatic delivery and asks for another explicit choice; Relay never falls back to the latest conversation. [Session selection and limits](docs/SESSIONS.md).

Files changing do not establish completion: explicit receipts do. External files are copied into managed storage; managed files are reused without redundant wrappers or result snapshots. Control JSON lives under a hidden `.relay` directory. Keep permanent deliverables in your project; expired managed files can be removed through cleanup.

</details>

## What it includes

| Area | Current behaviour |
| --- | --- |
| Task board | Direct CLI work requests or file handoffs, explicit conversations, completion plans and task controls |
| Sessions | SQLite bindings per task/attempt/turn/Agent, targeted discovery, display cache and fresh checks before resume |
| Agent entry | One name, one-click configuration, separate manual fields and a local configuration endpoint |
| Handoff rules | User-approved one-line entry, details on demand, removal preserving other edits |
| Connection map | Direction arrows and switches; node dots distinguish a recorded method, a failed probe and no method |
| CLI extensions | Native JSON/JSONL/text output adapters; separate new-session and resume probes |
| Settings | Relay location, retention, desktop alerts, selectable sounds, six languages and colour preferences |
| Windows tray | Minimize stays on the taskbar; × follows the remembered tray/exit choice. Tray Exit always closes the app |

GUI/notifications are Windows x64. Core CLI/MCP have Windows/Linux CI coverage and require .NET 10. There is no macOS desktop package, cloud relay or background service after all processes close. CLI output and most detailed documents are currently Chinese.

### Agent setup and rule removal

The Agents toolbar contains **New Agent**, **Edit Agent**, **Delete Agent**, **Agent CLI test** and **One-click setup**. Setup inspects CLI help and non-secret settings, reads the latest node directory, then submits through `agent setup --manifest` and imports a calling method when available. New setup never targets the selected node's ID. Edit changes the node you selected. If the local interface cannot run, use the manual fields rather than pasting a configuration block into the GUI.

Relay adapts supported native JSON, JSONL and final text outputs; the host does not have to emit Relay's private receipt format. Short probes and visibility confirmation are optional diagnostics, not prerequisites for delivery. Enable the desired direction in Connections after configuring its command. Resuming an existing conversation requires an exact binding, a fresh host-store check and a matching session ID in the response. A green node dot means a method is recorded; its tooltip states diagnostic status. Red means a failed probe, grey means no method. A session log or CLI executable path alone is not a complete calling method.

Source identity is automatic only when the calling adapter supplies exact session context. Relay does not ask models to discover IDs or make hidden conversations visible. Native create commands may still require a model configured in the host. Resume normally inherits that conversation's model and settings; Relay does not invent a common model or reasoning override.

The one-line rule consumes a little persistent context. Detailed instructions are read on demand; triggering depends on host compliance. Choose the actual host instruction file, not an assumed filename. Stable instruction links are refreshed after relocation.

Before deleting the portable software, use Settings → **Remove all handoff rules**, or run from the extracted package with .NET 10:

```powershell
dotnet .\relay.dll --home <relay-directory> guidance remove --all
```

Only Relay's marked blocks are removed. Deleting the ZIP or extracted folder cannot run uninstall cleanup.

<details>
<summary>CLI, MCP, development and architecture</summary>

`dotnet .\relay.dll --help` lists CLI commands. A host starts stdio MCP with `dotnet .\mcp\relay-mcp.dll --home <relay-directory> --agent <registered-id>`; the caller identity is fixed. The MCP server does not implement `--help`.

From a fresh clone with .NET 10 SDK:

```powershell
git clone https://github.com/jason-namehard/agent-relay.git
cd agent-relay
dotnet restore --locked-mode
dotnet test --configuration Release --no-restore
```

The solution covers Core, CLI, MCP and core tests. Windows GUI checks and packaging use PowerShell 7:

```powershell
dotnet restore src/AgentRelay.Notify/AgentRelay.Notify.csproj --locked-mode
pwsh ./scripts/test-desktop.ps1
pwsh ./scripts/check-licenses.ps1 -IncludeDesktop
pwsh ./scripts/package.ps1 -Flavor Windows
```

Scripts accept `-Dotnet <path>`. Packages go to `artifacts/packages`; existing archives are not overwritten. Regular tests use local fixtures, not paid model requests. Core owns the SQLite ledger and lifecycle; CLI/MCP and the four-page WPF app share it. Notify is an optional Windows helper.

[Setup and receipts](docs/CONFIGURATION_ASSISTANT.md) · [Sessions](docs/SESSIONS.md) · [UI spec](docs/UI_SPEC.md) · [Legacy protocol](docs/PROTOCOL.md) · [Advanced adapters](docs/ADAPTERS.md)

</details>

## Limits and verification

- Real host creation, visibility and continuation need testing in that host. Simulated bridges do not establish real integration; actual probes may incur model fees.
- Invalid or incomplete receipts need attention; saving a result alone never completes a task. Old message tasks retain their earlier CLI/MCP protocol.
- Deleting a task requires confirmation and removes its ledger history, retaining files for retention cleanup. Manually marking complete records a user decision; neither action verifies the Agent's work or stops an already running Agent.
- Relocation requires no unfinished task, including a return awaiting the other agent. Files and ledger are copied before switching. The old root is a backup and is not automatically cleaned; Check migration only resumes a recorded pending move.
- While Relay is running, a labelled in-app reminder appears at the bottom right. Windows receives a silent notification-center entry when available, with no duplicate system banner. Settings includes a notification test, sound selection and preview. Display scaling still needs manual checks.
- Agents must have access to the relay files; arbitrary remote or sandboxed sessions may not.

[Getting started](docs/GETTING_STARTED.md) · [Contributing](CONTRIBUTING.md) · [Changelog](CHANGELOG.md) · [Issues](https://github.com/jason-namehard/agent-relay/issues). Reports should include version, platform, steps and redacted errors.

## License

Project code is [MIT](LICENSE). Bundled .NET, Windows App SDK and other components retain their own terms: [distribution terms](DISTRIBUTION_TERMS.md), [third-party notices](THIRD_PARTY_NOTICES.md) and `licenses/`.
