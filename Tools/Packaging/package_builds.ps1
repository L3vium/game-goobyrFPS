<#
.SYNOPSIS
    Package the Hitmen player builds (Development -> Builds/Windows Dev, Shipping -> Builds/Windows).

.DESCRIPTION
    Runs RunUAT BuildCookRun with -NoCompileEditor (works with the editor open and Live Coding active),
    archives to a temporary folder without spaces (RunUAT breaks on "Windows Dev"), flattens the nested
    Windows/ folder and replaces the destination build.
    - The destination's Hitmen/Saved folder (crash reports, user settings) is kept.
    - Hitmen.pdb is copied to Builds/_Symbols/<Version>/<Config>/ so crash reports of this exact build can be
      symbolized later (Tools/Debug/hitmen_crash_report.py --binaries <that folder>, with the exe from Git).

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Packaging/package_builds.ps1 -Version 0.4.1
    powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Packaging/package_builds.ps1 -Version 0.4.1 -Config Dev
    Exit codes: 0 success, 1 RunUAT failed (destination untouched), 2 bad arguments.
#>
param(
    [Parameter(Mandatory = $true)] [string] $Version,
    [ValidateSet('Both', 'Dev', 'Shipping')] [string] $Config = 'Both'
)

$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$EngineRoot = if ($env:UE_ROOT) { $env:UE_ROOT } else { 'C:\Program Files\Epic Games\UE_5.7' }
$RunUAT = Join-Path $EngineRoot 'Engine\Build\BatchFiles\RunUAT.bat'
$Uproject = Join-Path $Root 'Hitmen.uproject'

$Targets = @()
if ($Config -in @('Both', 'Dev')) { $Targets += @{ Name = 'Dev'; Client = 'Development'; Temp = '_DevTmp'; Dest = 'Windows Dev' } }
if ($Config -in @('Both', 'Shipping')) { $Targets += @{ Name = 'Shipping'; Client = 'Shipping'; Temp = '_ShipTmp'; Dest = 'Windows' } }

foreach ($T in $Targets) {
    $Temp = Join-Path $Root ("Builds\" + $T.Temp)
    $Dest = Join-Path $Root ("Builds\" + $T.Dest)
    if (Test-Path $Temp) { Remove-Item -Recurse -Force $Temp }

    Write-Host "=== $($T.Name): BuildCookRun ($($T.Client)) -> $Temp"
    & $RunUAT BuildCookRun "-project=$Uproject" -platform=Win64 "-clientconfig=$($T.Client)" `
        -build -cook -stage -pak -archive "-archivedirectory=$Temp" -NoCompileEditor -utf8output -unattended -nop4
    if ($LASTEXITCODE -ne 0) {
        Write-Host "RunUAT failed for $($T.Name) (exit $LASTEXITCODE); $Dest left untouched."
        exit 1
    }

    $Staged = Join-Path $Temp 'Windows'
    if (-not (Test-Path $Staged)) { $Staged = $Temp }

    # Keep the destination's Saved folder (crash reports, settings) across the replacement.
    $SavedKeep = $null
    $DestSaved = Join-Path $Dest 'Hitmen\Saved'
    if (Test-Path $DestSaved) {
        $SavedKeep = Join-Path $Root ("Builds\" + $T.Temp + "_Saved")
        if (Test-Path $SavedKeep) { Remove-Item -Recurse -Force $SavedKeep }
        Move-Item $DestSaved $SavedKeep
    }
    # steam_appid.txt is written by the game at launch (non-shipping) and versioned: keep it too.
    $AppIdKeep = $null
    $DestAppId = Join-Path $Dest 'Hitmen\Binaries\Win64\steam_appid.txt'
    if (Test-Path $DestAppId) { $AppIdKeep = Get-Content $DestAppId -Raw }
    if (Test-Path $Dest) { Remove-Item -Recurse -Force $Dest }
    Move-Item $Staged $Dest
    if ($AppIdKeep -and -not (Test-Path $DestAppId)) { Set-Content -Path $DestAppId -Value $AppIdKeep -NoNewline -Encoding ascii }
    if ($SavedKeep) {
        $NewSaved = Join-Path $Dest 'Hitmen\Saved'
        if (Test-Path $NewSaved) { Remove-Item -Recurse -Force $NewSaved }
        Move-Item $SavedKeep $NewSaved
    }
    if (Test-Path $Temp) { Remove-Item -Recurse -Force $Temp }

    $Pdb = Join-Path $Dest 'Hitmen\Binaries\Win64\Hitmen*.pdb'
    $Symbols = Join-Path $Root ("Builds\_Symbols\" + $Version + "\" + $T.Name)
    New-Item -ItemType Directory -Force $Symbols | Out-Null
    Get-ChildItem $Pdb -ErrorAction SilentlyContinue | Copy-Item -Destination $Symbols -Force
    Get-ChildItem (Join-Path $Dest 'Hitmen\Binaries\Win64\Hitmen*.exe') | Copy-Item -Destination $Symbols -Force
    Write-Host "=== $($T.Name): done -> $Dest (symbols in $Symbols)"
}
exit 0
