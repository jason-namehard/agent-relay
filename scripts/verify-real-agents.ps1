param(
    [Parameter(Mandatory)][string]$Codex,
    [Parameter(Mandatory)][string]$Amq,
    [Parameter(Mandatory)][string]$ConfigPath,
    [string]$Dotnet = 'dotnet',
    [string]$WorkDirectory,
    [ValidateRange(10, 1800)][int]$TimeoutSeconds = 300
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($WorkDirectory)) {
    $WorkDirectory = Join-Path $projectRoot ('artifacts/real-agents/' + [guid]::NewGuid())
}
$WorkDirectory = [IO.Path]::GetFullPath($WorkDirectory)
New-Item -ItemType Directory -Path $WorkDirectory -Force | Out-Null
$dataRoot = Join-Path $WorkDirectory 'relay'
& $Dotnet build (Join-Path $projectRoot 'src/AgentRelay.Cli/AgentRelay.Cli.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
$cli = Join-Path $projectRoot 'src/AgentRelay.Cli/bin/Release/net10.0/relay.dll'
function Invoke-Relay([string[]]$Arguments) {
    $response = & $Dotnet $cli --home $dataRoot @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Relay command failed: $($response -join [Environment]::NewLine)" }
    return ($response -join [Environment]::NewLine | ConvertFrom-Json)
}
Invoke-Relay @('init') | Out-Null
foreach ($agent in @('agent-a', 'agent-b')) {
    Invoke-Relay @('agent','register','--id',$agent,'--host','codex','--config-path',$ConfigPath,'--cli-path',$Codex) | Out-Null
    Invoke-Relay @('agent','validate','--id',$agent) | Out-Null
    Invoke-Relay @('agent','verify-executor','--id',$agent) | Out-Null
}
Invoke-Relay @('permission','set','--from','agent-a','--to','agent-b','--decision','allow') | Out-Null
Invoke-Relay @('adapter','verify','--id','amq-cli','--executable',$Amq) | Out-Null
$schema = Join-Path $WorkDirectory 'request.schema.json'
$answer = Join-Path $WorkDirectory 'agent-a.json'
Set-Content -LiteralPath $schema -Encoding utf8 -Value @'
{"type":"object","properties":{"request_markdown":{"type":"string"}},"required":["request_markdown"],"additionalProperties":false}
'@
$start = [Diagnostics.ProcessStartInfo]::new([IO.Path]::GetFullPath($Codex))
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardInput = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$start.StandardInputEncoding = [Text.UTF8Encoding]::new($false)
$start.StandardOutputEncoding = [Text.UTF8Encoding]::new($false)
$start.StandardErrorEncoding = [Text.UTF8Encoding]::new($false)
$start.Environment['CODEX_HOME'] = Split-Path -Parent ([IO.Path]::GetFullPath($ConfigPath))
foreach ($argument in @('exec','--ignore-user-config','--ephemeral','--sandbox','read-only','--skip-git-repo-check',
    '--cd',$WorkDirectory,'--output-schema',$schema,'--output-last-message',$answer,'--json','-')) {
    $start.ArgumentList.Add($argument)
}
$agentA = [Diagnostics.Process]::Start($start)
$events = $agentA.StandardOutput.ReadToEndAsync()
$diagnostics = $agentA.StandardError.ReadToEndAsync()
$agentA.StandardInput.Write('不要调用工具。请给 Agent B 写一份简短中文任务：计算 4、9、16、25 的和，说明计算过程，返回标记 RELAY-PHASE2-54。你只写交接指令，不要自行解答。')
$agentA.StandardInput.Close()
if (-not $agentA.WaitForExit($TimeoutSeconds * 1000)) {
    $agentA.Kill($true)
    $agentA.WaitForExit()
    throw 'Agent A exceeded the execution timeout'
}
$agentAEvents = $events.GetAwaiter().GetResult()
Set-Content -LiteralPath (Join-Path $WorkDirectory 'agent-a-events.jsonl') -Encoding utf8 -Value $agentAEvents
Set-Content -LiteralPath (Join-Path $WorkDirectory 'agent-a-stderr.log') -Encoding utf8 -Value $diagnostics.GetAwaiter().GetResult()
if ($agentA.ExitCode -ne 0) { throw "Agent A exited with $($agentA.ExitCode)" }
$agentA.Dispose()
$instruction = Get-Content -LiteralPath $answer -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($instruction.request_markdown)) { throw 'Agent A did not provide a handoff' }
$request = Join-Path $WorkDirectory 'request-from-agent-a.md'
Set-Content -LiteralPath $request -Encoding utf8 -Value $instruction.request_markdown
$clock = [Diagnostics.Stopwatch]::StartNew()
$dispatch = Invoke-Relay @('task','dispatch','--from','agent-a','--to','agent-b','--request',$request,
    '--mode','auto','--idempotency-key','real-flow-1','--execute','--execution-timeout-seconds',"$TimeoutSeconds")
$clock.Stop()
if ($clock.Elapsed.TotalSeconds -ge 10) { throw 'Dispatch did not return before task execution finished' }
$task = $dispatch.data.task
$workerPid = $dispatch.data.execution.worker_pid
# A bounded worker may finish before its process handle can be obtained; persisted task state remains authoritative.
$worker = Get-Process -Id $workerPid -ErrorAction SilentlyContinue
if ($null -ne $worker -and -not $worker.WaitForExit(($TimeoutSeconds + 45) * 1000)) { throw 'Worker did not exit within its execution bound' }
$final = Invoke-Relay @('task','status','--task',$task.task_id)
if ($final.state -ne 'succeeded') { throw "Task finished in state $($final.state): $($final.data.task.error_message)" }
$result = Get-Content -LiteralPath $final.data.task.result_path -Raw
if (-not $result.Contains('54') -or -not $result.Contains('RELAY-PHASE2-54')) { throw 'Agent B result failed the arithmetic acceptance case' }
$before = Invoke-Relay @('task','execution','--task',$task.task_id)
Invoke-Relay @('task','run','--task',$task.task_id,'--attempt',$task.attempt_id,'--sender','agent-b') | Out-Null
$after = Invoke-Relay @('task','execution','--task',$task.task_id)
if ($before.data.session_id -ne $after.data.session_id) { throw 'Duplicate run created another agent session' }
$aSession = @($agentAEvents -split "`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    ForEach-Object { $_ | ConvertFrom-Json } | Where-Object { $_.type -eq 'thread.started' })[0].thread_id
if ([string]::IsNullOrWhiteSpace($aSession) -or [string]::IsNullOrWhiteSpace($final.data.execution.session_id) -or $aSession -eq $final.data.execution.session_id) {
    throw 'Expected two distinct verified CLI sessions'
}
$summary = [pscustomobject]@{
    status = 'passed'
    task_id = $task.task_id
    agent_a_session = $aSession
    agent_b_session = $final.data.execution.session_id
    dispatch_return_seconds = $clock.Elapsed.TotalSeconds
    result = '54'
    duplicate_run = 'deduplicated'
}
$summary | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $WorkDirectory 'summary.json') -Encoding utf8
$summary
