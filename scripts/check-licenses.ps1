$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$seen = @{}
$allowed = @('MIT', 'Apache-2.0')
foreach ($project in @('src/AgentRelay.Core', 'src/AgentRelay.Cli', 'tests/AgentRelay.Core.Tests')) {
    $assetsPath = Join-Path $projectRoot "$project/obj/project.assets.json"
    $assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json
    foreach ($library in $assets.libraries.PSObject.Properties) {
        if ($library.Value.type -ne 'package' -or $seen.ContainsKey($library.Name)) { continue }
        $packagePath = $null
        foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
            $candidate = Join-Path $folder $library.Value.path
            if (Test-Path -LiteralPath $candidate) { $packagePath = $candidate; break }
        }
        if ($null -eq $packagePath) { throw "Restored package not found: $($library.Name)" }
        $specs = @(Get-ChildItem -LiteralPath $packagePath -Filter '*.nuspec')
        if ($specs.Count -ne 1) { throw "Expected one package manifest: $($library.Name)" }
        $spec = $specs[0]
        [xml]$metadata = Get-Content -LiteralPath $spec.FullName -Raw
        $license = $metadata.package.metadata.license
        if ($null -eq $license -or $license.type -ne 'expression' -or $license.InnerText -notin $allowed) {
            throw "Review required for package license: $($library.Name)"
        }
        $seen[$library.Name] = $license.InnerText
    }
}
$seen.GetEnumerator() | Sort-Object Name | ForEach-Object {
    [pscustomobject]@{ Package = $_.Name; License = $_.Value }
}
