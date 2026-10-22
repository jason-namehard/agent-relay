param([string]$Dotnet = 'dotnet', [switch]$IncludeDesktop)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$destination = Join-Path $projectRoot 'artifacts/relay'
& $Dotnet publish (Join-Path $projectRoot 'src/AgentRelay.Cli/AgentRelay.Cli.csproj') -c Release --no-restore -o $destination
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
& $Dotnet publish (Join-Path $projectRoot 'src/AgentRelay.Mcp/AgentRelay.Mcp.csproj') -c Release --no-restore -o (Join-Path $destination 'mcp')
if ($LASTEXITCODE -ne 0) { throw 'MCP publish failed' }
if ($IncludeDesktop) {
    if (-not $IsWindows) { throw 'The desktop helper must be built on Windows' }
    & $Dotnet publish (Join-Path $projectRoot 'src/AgentRelay.Notify/AgentRelay.Notify.csproj') -c Release --no-restore -o (Join-Path $destination 'desktop')
    if ($LASTEXITCODE -ne 0) { throw 'Desktop publish failed' }
    $assets = Get-Content (Join-Path $projectRoot 'src/AgentRelay.Notify/obj/project.assets.json') -Raw | ConvertFrom-Json
    $runtimeRoot = Join-Path $destination 'licenses/dotnet-runtime'
    New-Item -ItemType Directory -Path $runtimeRoot -Force | Out-Null
    foreach ($pack in $assets.project.frameworks.PSObject.Properties.Value.downloadDependencies) {
        if ($pack.name -notmatch '^Microsoft\.(NETCore|WindowsDesktop)\.App\.Runtime\.win-x64$') { continue }
        $version = $pack.version.Trim('[',']').Split(',')[0].Trim()
        $package = $null
        foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
            $candidate = Join-Path $folder ($pack.name.ToLowerInvariant() + '/' + $version)
            if (Test-Path $candidate) { $package = $candidate; break }
        }
        if ($null -eq $package) { throw "Runtime package missing: $($pack.name)" }
        $target = Join-Path $runtimeRoot ($pack.name + '/' + $version)
        New-Item -ItemType Directory -Path $target -Force | Out-Null
        foreach ($file in Get-ChildItem $package -File | Where-Object {$_.Name -match 'LICENSE|THIRD.PARTY.NOTICES'}) {
            Copy-Item -LiteralPath $file.FullName -Destination $target
        }
    }
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE'), (Join-Path $projectRoot 'THIRD_PARTY_NOTICES.md'), (Join-Path $projectRoot 'DISTRIBUTION_TERMS.md') -Destination $destination
Copy-Item -LiteralPath (Join-Path $projectRoot 'licenses') -Destination $destination -Recurse -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/PACKAGE_USAGE.md') -Destination (Join-Path $destination 'README.md')
Write-Output $destination
