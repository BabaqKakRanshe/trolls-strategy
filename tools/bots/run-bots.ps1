<#
.SYNOPSIS
    Plays the campaign with the bots' population in Unity batchmode and opens the statistics page.

.DESCRIPTION
    Runs TrollStrategy.Bots.BotMenu.RunAllBatch on unity/TrollStategy: personas 1..Count (BotPopulation) on
    every core at once. It writes Builds/Stats/bots: index.html (the report page), bots-data.js (the latest
    run), history.jsonl (earlier runs), summary.md, population.csv (a line per bot) and runs/ (full reports of
    the bots that did not finish and of the fastest, median and slowest), and the hub Builds/Stats/index.html
    over the bots' and the players' pages (tools/stats/players.py). An open page picks the new run up on its own.

    The Unity editor must be closed for this project: two editors cannot open one project. With the editor
    open, use the menu TrollStrategy > Bots > Run Campaign Bots instead.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\bots\run-bots.ps1
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\bots\run-bots.ps1 -NoOpen
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\bots\run-bots.ps1 -Count 1000
#>
param(
    # Unity.exe to use; by default the Hub install of the project's editor version.
    [string]$Unity,
    # Do not open the statistics page when the run is done.
    [switch]$NoOpen,
    # Personas to play: 200 for a check, 1000 for a night or a release.
    [int]$Count = 200,
    # Batch runs to try when Unity starts without its licence (it then compiles nothing).
    [int]$Attempts = 3
)

$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$project = Join-Path $repo 'unity\TrollStategy'
$page = Join-Path $project 'Builds\Stats\index.html'
$log = Join-Path $project 'Builds\Stats\bots\batch.log'

if (-not $Unity) {
    $versionLine = Select-String -Path (Join-Path $project 'ProjectSettings\ProjectVersion.txt') -Pattern '^m_EditorVersion:\s*(\S+)'
    $version = $versionLine.Matches[0].Groups[1].Value
    # the folders editors live in: the Hub's default, its install path, and wherever the editors it lists sit
    # (an editor moved by hand stays next to the others but may be missing from the Hub's list)
    $roots = @('C:\Program Files\Unity\Hub\Editor')
    $hub = Join-Path $env:APPDATA 'UnityHub'
    $installPath = Join-Path $hub 'secondaryInstallPath.json'
    if (Test-Path $installPath) {
        $path = (Get-Content $installPath -Raw).Trim().Trim('"') -replace '\\\\', '\'
        if ($path) { $roots += $path, (Join-Path $path 'Hub\Editor') }
    }
    $hubEditors = Join-Path $hub 'editors-v2.json'
    if (Test-Path $hubEditors) {
        # read with a pattern: the file has keys differing only in case, which ConvertFrom-Json refuses
        foreach ($match in [regex]::Matches((Get-Content $hubEditors -Raw), '"location"\s*:\s*\[\s*"([^"]+Unity\.exe)"')) {
            $roots += Split-Path (Split-Path (Split-Path ($match.Groups[1].Value -replace '\\\\', '\')))
        }
    }
    $Unity = $roots | Select-Object -Unique | ForEach-Object { Join-Path $_ "$version\Editor\Unity.exe" } |
        Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $Unity) { throw "Unity $version not found in Unity Hub's list or under C:\Program Files (pass -Unity <path to Unity.exe>)" }
}
if (-not (Test-Path $Unity)) { throw "Unity not found: $Unity (pass -Unity <path to Unity.exe>)" }

$open = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" |
    Where-Object { $_.CommandLine -and $_.CommandLine -like "*$project*" -and $_.CommandLine -notlike '*AssetImportWorker*' }
if ($open) {
    throw "The Unity editor has this project open (pid $($open[0].ProcessId)). Close it, or use TrollStrategy > Bots > Run Campaign Bots."
}

New-Item -ItemType Directory -Force -Path (Split-Path $log) | Out-Null
for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
    Write-Host "Bots: batch run $attempt of $Attempts..."
    $process = Start-Process -FilePath $Unity -Wait -PassThru -ArgumentList @(
        '-batchmode', '-nographics', '-projectPath', "`"$project`"",
        '-executeMethod', 'TrollStrategy.Bots.BotMenu.RunAllBatch', '-botCount', $Count, '-logFile', "`"$log`"")
    $text = if (Test-Path $log) { Get-Content $log -Raw } else { '' }
    if ($process.ExitCode -eq 0 -and $text -match '\[Bots\] report page') { break }
    if ($text -match 'not registered because your license') {
        Write-Host 'Bots: Unity started without its licence; trying again.'
        continue
    }
    throw "Bots: the batch run failed (exit $($process.ExitCode)); see $log"
}
if ($attempt -gt $Attempts) { throw "Bots: Unity never got its licence; see $log" }

# the hub compares the run with the rules as they stand
$codeState = Join-Path $PSScriptRoot '..\stats\code_state.py'
try { & python $codeState | Out-Host } catch { Write-Host "Bots: no python for $codeState; the hub cannot tell whether the run is current." }

Write-Host "Bots: statistics page $page"
if (-not $NoOpen) { Start-Process $page }
