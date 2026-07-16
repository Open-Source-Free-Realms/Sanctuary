[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ServerPath
)

$ErrorActionPreference = 'Stop'
$resolvedServerPath = (Resolve-Path -LiteralPath $ServerPath).Path
$serverText = [IO.File]::ReadAllText($resolvedServerPath)
$manifestMarker = 'const assetManifestOverrides = new Map(['
$markerIndex = $serverText.IndexOf($manifestMarker, [StringComparison]::Ordinal)

if ($markerIndex -lt 0) {
    throw "Could not find AuthBridge's assetManifestOverrides map in $resolvedServerPath"
}

$entries = @(
    '    ["enforcer_m.adr.z", { crc: 4121300151, size: 320, source: "enforcer_m.adr.z" }],'
    '    ["enforcer_m.dsk.z", { crc: 2820031799, size: 1812, source: "enforcer_m.dsk.z" }],'
    '    ["enforcer_m_body_lod0.dme.z", { crc: 1767807266, size: 30554, source: "enforcer_m_body_lod0.dme.z" }],'
    '    ["enforcer_m.dma.z", { crc: 3196373089, size: 138, source: "enforcer_m.dma.z" }],'
    '    ["enforcer_m.dds.z", { crc: 1195088484, size: 35101, source: "enforcer_m.dds.z" }],'
    '    ["enforcer_m_chest_jacket.adr.z", { crc: 1078437424, size: 96, source: "enforcer_m_chest_jacket.adr.z" }],'
    '    ["enforcer_m_chest_jacket.dme.z", { crc: 1677086355, size: 11982, source: "enforcer_m_chest_jacket.dme.z" }],'
    '    ["enforcer_m_chest_jacket.dma.z", { crc: 4275069641, size: 150, source: "enforcer_m_chest_jacket.dma.z" }],'
    '    ["enforcer_m_chest_jacket.dds.z", { crc: 3457467616, size: 26669, source: "enforcer_m_chest_jacket.dds.z" }],'
    '    ["enforcer_m_legs_pants.adr.z", { crc: 2694274828, size: 95, source: "enforcer_m_legs_pants.adr.z" }],'
    '    ["enforcer_m_legs_pants.dme.z", { crc: 803653387, size: 6916, source: "enforcer_m_legs_pants.dme.z" }],'
    '    ["enforcer_m_legs_pants.dma.z", { crc: 692196500, size: 153, source: "enforcer_m_legs_pants.dma.z" }],'
    '    ["enforcer_m_legs_pants.dds.z", { crc: 2476267323, size: 8450, source: "enforcer_m_legs_pants.dds.z" }],'
    '    ["enforcer_loc_walk.gr2.z", { crc: 2793589749, size: 13578, source: "enforcer_loc_walk.gr2.z" }],'
    '    ["enforcer_loc_run.gr2.z", { crc: 31130863, size: 14439, source: "enforcer_loc_run.gr2.z" }],'
    '    ["enforcer_loc_backpedal.gr2.z", { crc: 2924350686, size: 19511, source: "enforcer_loc_backpedal.gr2.z" }],'
)

$missingEntries = @(
    foreach ($entry in $entries) {
        if ($entry -notmatch '\["([^"]+)"') {
            throw "Could not parse installer entry: $entry"
        }

        $assetName = $Matches[1]
        $needle = '["' + $assetName + '"'
        if ($serverText.IndexOf($needle, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            $entry
        }
    }
)

if ($missingEntries.Count -eq 0) {
    Write-Host 'AuthBridge already contains all 16 Enforcer manifest overrides. No changes were made.'
    exit 0
}

$newLine = if ($serverText.Contains("`r`n")) { "`r`n" } else { "`n" }
$insertAt = $markerIndex + $manifestMarker.Length
$blockLines = @(
    '    // Authentic Enforcer model, uniform, and locomotion files.'
    '    // AuthBridge wraps these raw loose assets, so publish the generated FreeRealms-Z sizes.'
) + $missingEntries
$insertBlock = $newLine + ($blockLines -join $newLine)
$updatedServerText = $serverText.Insert($insertAt, $insertBlock)
$timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$backupPath = "$resolvedServerPath.enforcer-backup-$timestamp"

Copy-Item -LiteralPath $resolvedServerPath -Destination $backupPath
$utf8WithoutBom = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllText($resolvedServerPath, $updatedServerText, $utf8WithoutBom)

Write-Host "Added $($missingEntries.Count) missing Enforcer manifest override(s)."
Write-Host "Backup: $backupPath"
