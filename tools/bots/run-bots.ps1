<#
.SYNOPSIS
    Plays the campaign with every bot profile in Unity batchmode and opens the statistics page.

.DESCRIPTION
    Runs TrollStrategy.Bots.BotMenu.RunAllBatch on unity/TrollStategy, which writes Builds/Stats/bots:
    index.html (the report page), bots-data.js (the latest runs), history.jsonl (earlier runs), and a
    Markdown and CSV report per profile, and the hub Builds/Stats/index.html over the bots' and the players'
    pages (tools/stats/players.py). An open page picks the new run up on its own.

    The Unity editor must be closed for this project: two editors cannot open one project. With the editor
    open, use the menu TrollStrategy > Bots > Run Campaign Bots instead.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\bots\run-bots.ps1
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\bots\run-bots.ps1 -NoOpen
#>
param(
    # Unity.exe to use; by default the Hub install of the project's editor version.
    [string]$Unity,
    # Do not open the statistics page when the run is done.
    [switch]$NoOpen,
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
    $Unity = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
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
        '-executeMethod', 'TrollStrategy.Bots.BotMenu.RunAllBatch', '-logFile', "`"$log`"")
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
