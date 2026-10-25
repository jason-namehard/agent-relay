# Agent Relay

**English** | [简体中文](README.zh.md)

[![CI](https://github.com/jason-namehard/agent-relay/actions/workflows/ci.yml/badge.svg)](https://github.com/jason-namehard/agent-relay/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A local handoff desk for people working with several AI agents. Pass a file from A to B, see who needs to act next, and finish without unnecessary back-and-forth.

## Quick start — Windows x64

Public preview: `0.1.0-preview.10`. In Windows PowerShell 5.1 or PowerShell 7, download and extract the whole package into a new folder:

```powershell
$version = 'v0.1.0-preview.10'
$zip = "AgentRelay-$version-windows-x64.zip"
$download = Join-Path $PWD $zip
$ProgressPreference = 'SilentlyContinue'
Invoke-WebRequest "https://github.com/jason-namehard/agent-relay/releases/download/$version/$zip" -UseBasicParsing -OutFile $download
Expand-Archive -LiteralPath $download -DestinationPath ".\AgentRelay-$version"
Set-Location ".\AgentRelay-$version"
.\gui\AgentRelay.Desktop.exe
```

Or download from [Releases](https://github.com/jason-namehard/agent-relay/releases/tag/v0.1.0-preview.10). `TryDemo.cmd` opens isolated samples without model calls; they do not prove real host integration. GUI and notifications bundle their runtime. CLI/MCP require [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0). There is no installer or code signing; Windows may show a security warning.

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

## A file handoff in practice

1. **Add A and B** in Agents. Choose your own names; roles and CLI paths are optional.
2. **Optionally install the handoff rule.** Choose the instruction file the host actually loads, inspect the one-line addition and confirm. Detailed instructions are read when needed; existing sessions may need to reread their instructions.
3. **Ask A to write a file for B.** The installed rule can make A return short handoff JSON. Without a rule, append the format copied from New handoff to your request. Paste the JSON and choose **Parse and create handoff**, or select a file yourself.
4. **Copy the prompt for B** and paste it into B's real session. The short prompt points to task context containing the file, work request and receipt template.
5. **B writes a receipt.** While the app is open, receipt file events update the task. Startup and Refresh also read outstanding receipts; no manual reply import is required for this workflow.
6. **Continue only if work remains.** `done` with `needs_reply: false` ends the task. A return needs a real result file and concrete `next_action`; Relay then offers the prompt for the other agent. After four turns it stops further relay for a human decision.

The new GUI **file handoff is manual** in this release. Existing CLI/MCP automatic message delivery and advanced probes remain separate; the file dialog does not offer automatic delivery.

Files changing do not establish completion: explicit receipts do. External files are copied into managed storage; managed files are reused without redundant wrappers or result snapshots. Control JSON lives under a hidden `.relay` directory. Keep permanent deliverables in your project; expired managed files can be removed through cleanup.

## What it includes

| Area | Current behaviour |
| --- | --- |
| Task board | File handoffs, named waiting/working states, next-person copy action and history |
| Agent entry | User names, optional paths, JSON draft parsing or a local configuration endpoint |
| Handoff rules | User-approved one-line entry, details on demand, removal preserving other edits |
| Connection map | One line per pair; arrows show recipe directions, green/red show enabled/paused |
| CLI extensions | Save existing recipes without executing them; separate new-session and resume probes |
| Settings | Relay location, retention, notifications, six languages, four palettes and accent colour |

GUI/notifications are Windows x64. Core CLI/MCP have Windows/Linux CI coverage and require .NET 10. There is no macOS desktop package, cloud relay or background service after all processes close. CLI output and most detailed documents are currently Chinese.

### Agent setup and rule removal

The setup prompt explains how to inspect CLI help and non-secret settings, then submit configuration through `agent setup --manifest`. The GUI supplies an actual CLI command when found; otherwise the agent returns JSON for parsing. A session ID or conversation log is not an invocation method. JSON cannot create a missing bridge, and no untested host compatibility is claimed.

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

[Setup and receipts](docs/CONFIGURATION_ASSISTANT.md) · [UI spec](docs/UI_SPEC.md) · [Legacy protocol](docs/PROTOCOL.md) · [Advanced adapters](docs/ADAPTERS.md)

</details>

## Limits and verification

- Real host creation, visibility and continuation need testing in that host. Simulated bridges do not establish real integration; actual probes may incur model fees.
- Invalid or incomplete receipts need attention; saving a result alone never completes a task. Old message tasks retain their earlier CLI/MCP protocol.
- Relocation requires no unfinished task, including a return awaiting the other agent. Files and ledger are copied before switching. The old root is a backup and is not automatically cleaned; Check migration only resumes a recorded pending move.
- Notification saving has automated coverage; real Notification Center pop-ups and 125%/150% scaling still need manual checks.
- Agents must have access to the relay files; arbitrary remote or sandboxed sessions may not.

[Getting started](docs/GETTING_STARTED.md) · [Contributing](CONTRIBUTING.md) · [Changelog](CHANGELOG.md) · [Issues](https://github.com/jason-namehard/agent-relay/issues). Reports should include version, platform, steps and redacted errors.

## License

Project code is [MIT](LICENSE). Bundled .NET, Windows App SDK and other components retain their own terms: [distribution terms](DISTRIBUTION_TERMS.md), [third-party notices](THIRD_PARTY_NOTICES.md) and `licenses/`.
