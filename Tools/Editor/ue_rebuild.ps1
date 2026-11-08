<#
.SYNOPSIS
    Compile the Hitmen C++ without ever leaving the editor closed.

.DESCRIPTION
    -Mode auto (default) picks the cheapest safe path:
      * live : only .cpp files changed and the editor is open -> Live Coding patches the running
               editor (LiveCoding.Compile). Nothing is closed.
      * full : a header, Build.cs/Target.cs, the .uproject or a new source file changed (new
               UCLASS/UPROPERTY/UFUNCTION or class layout), or the editor is closed ->
               stop PIE, save dirty packages, close the editor, Build.bat, reopen the editor.
    The editor is reopened in a finally block: a failed build, an error or Ctrl+C still reopens it.
    If the editor refuses to close, nothing is killed and nothing is built (exit 2).

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Editor/ue_rebuild.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Editor/ue_rebuild.ps1 -Mode full
    Exit codes: 0 success, 1 compile failed (editor reopened on the previous binaries),
                2 editor did not close / unsaved packages with -NoSave, 3 editor did not come back.
#>
param(
    [ValidateSet('auto', 'live', 'full')] [string] $Mode = 'auto',
    [switch] $NoSave,     # abort instead of saving dirty packages before closing
    [switch] $NoReopen,   # leave the editor closed after a full build (CI only)
    [switch] $FullOnError, # Live Coding failed -> full rebuild to print the compiler errors (agents)
    [int] $ReadyTimeout = 420
)

$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Uproject = Join-Path $Root 'Hitmen.uproject'
$EngineRoot = if ($env:UE_ROOT) { $env:UE_ROOT } else { 'C:\Program Files\Epic Games\UE_5.7' }
$EditorExe = Join-Path $EngineRoot 'Engine\Binaries\Win64\UnrealEditor.exe'
$BuildBat = Join-Path $EngineRoot 'Engine\Build\BatchFiles\Build.bat'
$Remote = Join-Path $PSScriptRoot 'ue_remote.py'
$EditorLog = Join-Path $Root 'Saved\Logs\Hitmen.log'
$LiveConsoleLog = Join-Path $EngineRoot 'Engine\Programs\LiveCodingConsole\Saved\Logs\LiveCodingConsole.log'
$BuildLog = Join-Path $Root 'Saved\Logs\HitmenRebuild.log'
$Stamp = Join-Path $Root 'Intermediate\HitmenRebuild.stamp'

function Say([string] $Text) { Write-Host "[rebuild] $Text" }

function Get-HitmenEditor {
    Get-CimInstance Win32_Process -Filter "Name='UnrealEditor.exe'" |
        Where-Object { $_.CommandLine -like '*Hitmen.uproject*' }
}

# Runs Python in the open editor; returns @{ Code; Lines }.
function Invoke-EditorPython([string] $Code) {
    $out = & python $Remote $Code 2>&1 | ForEach-Object { "$_" }
    return @{ Code = $LASTEXITCODE; Lines = @($out) }
}

function Get-Value($Result, [string] $Key) {
    $line = $Result.Lines | Where-Object { $_ -like "$Key=*" } | Select-Object -First 1
    if ($line) { return $line.Substring($Key.Length + 1) } else { return $null }
}

function Read-LogFrom([string] $Path, [long] $Offset) {
    if (-not (Test-Path $Path)) { return '' }
    $stream = [System.IO.File]::Open($Path, 'Open', 'Read', 'ReadWrite')
    try {
        if ($Offset -gt $stream.Length) { $Offset = 0 }
        [void] $stream.Seek($Offset, 'Begin')
        return (New-Object System.IO.StreamReader($stream)).ReadToEnd()
    } finally { $stream.Dispose() }
}

function Get-LogLength([string] $Path) {
    if (Test-Path $Path) { return (Get-Item $Path).Length } else { return 0 }
}

# A full build is needed when anything Live Coding cannot patch changed since the last build.
function Get-FileTime([string] $Path) {
    if (Test-Path $Path) { return (Get-Item $Path).LastWriteTime } else { return [datetime]::MinValue }
}

function Test-NeedsFullBuild {
    # Each module is compared with its own DLL (UBT does not relink an unchanged module) or with
    # the stamp of the last successful full build, whichever is newer.
    $stamp = Get-FileTime $Stamp
    $structural = @()
    $oldest = [datetime]::MaxValue
    foreach ($dir in Get-ChildItem (Join-Path $Root 'Source') -Directory) {
        $dll = Join-Path $Root "Binaries\Win64\UnrealEditor-$($dir.Name).dll"
        if (-not (Test-Path $dll)) { Say "No binaries for $($dir.Name) yet."; return $true }
        $reference = @((Get-FileTime $dll), $stamp) | Sort-Object | Select-Object -Last 1
        if ($reference -lt $oldest) { $oldest = $reference }
        $structural += @(Get-ChildItem $dir.FullName -Recurse -File |
            Where-Object { $_.LastWriteTime -gt $reference -and $_.Extension -in '.h', '.hpp', '.inl', '.cs' })
    }
    $structural += @(Get-ChildItem (Join-Path $Root 'Source') -File -Filter '*.cs' | Where-Object { $_.LastWriteTime -gt $oldest })
    if ((Get-FileTime $Uproject) -gt $oldest) { $structural += Get-Item $Uproject }
    Push-Location $Root
    try { $untracked = @(git ls-files --others --exclude-standard -- Source 2>$null) } finally { Pop-Location }
    if ($structural.Count -gt 0) {
        Say ("Structural change: " + (($structural | Select-Object -First 5 | ForEach-Object Name) -join ', '))
        return $true
    }
    if ($untracked.Count -gt 0) { Say ("New source files: " + ($untracked -join ', ')); return $true }
    return $false
}

function Wait-EditorReady {
    Say "Waiting for the editor to answer (up to $ReadyTimeout s)..."
    $deadline = (Get-Date).AddSeconds($ReadyTimeout)
    while ((Get-Date) -lt $deadline) {
        if (-not (Get-HitmenEditor)) { Start-Sleep -Seconds 3; continue }
        $ping = Invoke-EditorPython "print('READY=1')"
        if ((Get-Value $ping 'READY') -eq '1') { Say 'Editor is open and ready (MCP and remote execution available).'; return $true }
        Start-Sleep -Seconds 5
    }
    Say 'WARNING: the editor did not answer in time. Check it manually.'
    return $false
}

function Start-HitmenEditor {
    if (Get-HitmenEditor) { return }
    Say 'Reopening the editor...'
    Start-Process -FilePath $EditorExe -ArgumentList "`"$Uproject`""
}

# ---------------------------------------------------------------- live coding
# The Live Coding console flushes its log lazily: wait a little for the compiler errors.
function Show-LiveCodingErrors([long] $Offset) {
    for ($i = 0; $i -lt 10; $i++) {
        $errors = (Read-LogFrom $LiveConsoleLog $Offset) -split "`r?`n" |
            Where-Object { $_ -match 'error C\d+|error LNK\d+|: (fatal )?error ' } | Select-Object -Unique -First 25
        if ($errors) { $errors | ForEach-Object { Write-Host "  $_" }; return }
        Start-Sleep -Seconds 1
    }
    Say 'Compiler errors are in the Live Coding console window (Ctrl+Alt+F11 in the editor), or run -Mode full.'
}

function Invoke-LiveCoding {
    Say 'Live Coding compile (editor stays open)...'
    $offset = Get-LogLength $EditorLog
    $consoleOffset = Get-LogLength $LiveConsoleLog
    $r = Invoke-EditorPython "import unreal`nworld = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()`nunreal.SystemLibrary.execute_console_command(world, 'LiveCoding.Compile')`nprint('SENT=1')"
    if ((Get-Value $r 'SENT') -ne '1') { Say ('Could not reach the editor: ' + ($r.Lines -join ' ')); return $null }
    $deadline = (Get-Date).AddSeconds(600)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 2
        $text = Read-LogFrom $EditorLog $offset
        if ($text -match 'LogLiveCoding: (Display|Warning): (Live coding succeeded[^\r\n]*)') { Say $Matches[2]; return $true }
        if ($text -match 'LogLiveCoding: Error: ([^\r\n]*)') {
            Say ("Live Coding: " + $Matches[1])
            if ($text -match 'Object structure changes detected') { return 'structural' }
            Show-LiveCodingErrors $consoleOffset
            return $false
        }
    }
    Say 'Live Coding did not report a result within 10 minutes.'
    return $false
}

# ---------------------------------------------------------------- full rebuild
function Close-HitmenEditor {
    if (-not (Get-HitmenEditor)) { return $true }
    $status = Invoke-EditorPython @"
import unreal
pie = unreal.EditorLevelLibrary.get_game_world() is not None
if pie:
    unreal.get_editor_subsystem(unreal.LevelEditorSubsystem).editor_request_end_play()
print('PIE=' + ('1' if pie else '0'))
"@
    if ($status.Code -ne 0) { Say ('Editor is running but does not answer remote execution: ' + ($status.Lines -join ' ')); return $false }
    if ((Get-Value $status 'PIE') -eq '1') {
        Say 'Stopping Play In Editor...'
        for ($i = 0; $i -lt 30; $i++) {
            Start-Sleep -Seconds 1
            if ((Get-Value (Invoke-EditorPython "import unreal`nprint('PIE=' + ('1' if unreal.EditorLevelLibrary.get_game_world() else '0'))") 'PIE') -eq '0') { break }
        }
    }
    $dirty = Invoke-EditorPython @"
import unreal
utils = unreal.EditorLoadingAndSavingUtils
names = [p.get_name() for p in list(utils.get_dirty_map_packages()) + list(utils.get_dirty_content_packages())]
print('DIRTY=' + ';'.join(names))
"@
    $dirtyList = Get-Value $dirty 'DIRTY'
    if ($dirtyList) {
        if ($NoSave) { Say "Unsaved packages, nothing closed (-NoSave): $dirtyList"; return $false }
        Say "Saving: $dirtyList"
        $saved = Invoke-EditorPython "import unreal`nprint('SAVED=' + str(unreal.EditorLoadingAndSavingUtils.save_dirty_packages(True, True)))"
        if ((Get-Value $saved 'SAVED') -ne 'True') { Say 'Saving failed, the editor stays open.'; return $false }
    }
    Say 'Closing the editor...'
    [void] (Invoke-EditorPython "import unreal`nunreal.SystemLibrary.quit_editor()")
    for ($i = 0; $i -lt 120; $i++) {
        if (-not (Get-HitmenEditor)) { Say 'Editor closed.'; return $true }
        Start-Sleep -Seconds 1
    }
    Say 'The editor did not close within 2 minutes (a dialog may be waiting). Nothing was killed.'
    return $false
}

function Invoke-FullBuild {
    Get-Process LiveCodingConsole -ErrorAction SilentlyContinue | Stop-Process -Force
    Say "Building HitmenEditor (log: $BuildLog)..."
    New-Item -ItemType Directory -Force (Split-Path $BuildLog) | Out-Null
    $errLog = "$BuildLog.err"
    $p = Start-Process -FilePath $BuildBat -NoNewWindow -Wait -PassThru `
        -RedirectStandardOutput $BuildLog -RedirectStandardError $errLog `
        -ArgumentList @('HitmenEditor', 'Win64', 'Development', "-Project=`"$Uproject`"", '-WaitMutex', '-NoHotReload')
    $lines = @(Get-Content $BuildLog) + @(Get-Content $errLog -ErrorAction SilentlyContinue)
    $errors = $lines | Where-Object { $_ -match ': (fatal )?error |error C\d+|error LNK\d+|ERROR:' } | Select-Object -First 25
    if ($p.ExitCode -eq 0) {
        Set-Content -Path $Stamp -Value (Get-Date -Format o)
        Say 'Build succeeded.'
        return $true
    }
    Say "Build FAILED (exit $($p.ExitCode)). The editor reopens on the previous binaries."
    $errors | ForEach-Object { Write-Host "  $_" }
    return $false
}

# ---------------------------------------------------------------- main
$editorOpen = [bool] (Get-HitmenEditor)
if ($Mode -eq 'auto') {
    if (-not $editorOpen) { $Mode = 'full'; Say 'Editor closed: full build.' }
    elseif (Test-NeedsFullBuild) { $Mode = 'full' }
    else { $Mode = 'live' }
}
if ($Mode -eq 'live' -and -not $editorOpen) { Say 'Live Coding needs the open editor: switching to a full build.'; $Mode = 'full' }

if ($Mode -eq 'live') {
    $result = Invoke-LiveCoding
    if ($result -eq $true) { exit 0 }
    # Compile error: the editor stays open (errors in the Live Coding window) unless -FullOnError
    # asks for a full rebuild, which prints the compiler errors and reopens the editor.
    if ($result -ne 'structural' -and $null -ne $result -and -not $FullOnError) { exit 1 }
    Say 'Falling back to a full rebuild.'
}

$exitCode = 0
$closed = Close-HitmenEditor
if (-not $closed) { exit 2 }
try {
    if (-not (Invoke-FullBuild)) { $exitCode = 1 }
}
finally {
    if (-not $NoReopen) {
        Start-HitmenEditor
        if (-not (Wait-EditorReady) -and $exitCode -eq 0) { $exitCode = 3 }
    }
}
exit $exitCode
